using Application.Abstractions.Inventory;
using Application.Abstractions.Payments;
using Application.Abstractions.Persistence;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Infrastructure.BackgroundJobs;

/// <summary>
/// Layer 3 of the Strategy Handover Race defense: an automated
/// reconciliation loop that detects oversold inventory and compensates.
///
/// Every 30 seconds the job scans all high-demand events (IsHighDemand=1),
/// compares each TicketType's Redis counter against the actual SQL
/// AvailableCount fetched in the same iteration. If the Redis counter
/// is LESS than the SQL AvailableCount, true overselling has occurred
/// (e.g., Redis was seeded with 100 but SQL dropped to 95 due to
/// concurrent successful requests that bypassed the Redis counter).
/// The job cancels the most recent pending bookings for that ticket
/// type and stages a durable <c>CompensateOversoldBooking</c>
/// compensation log.
/// </summary>
public sealed class InventoryReconciliationJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InventoryReconciliationJob> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(30);
    private const int BatchSize = 50;

    private const string InventoryKeyPrefix = "inv:ticket:";

    public InventoryReconciliationJob(
        IServiceProvider serviceProvider,
        ILogger<InventoryReconciliationJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Inventory Reconciliation Job started");

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Inventory Reconciliation Job startup delay cancelled");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcileAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in Inventory Reconciliation Job");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("Inventory Reconciliation Job stopped");
    }

    private async Task ReconcileAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var eventRepo = scope.ServiceProvider.GetRequiredService<IEventRepository>();
        var bookingRepo = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var compensationRepo = scope.ServiceProvider.GetRequiredService<ICompensationLogRepository>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var redis = scope.ServiceProvider.GetRequiredService<ConnectionMultiplexer>();

        var db = redis.GetDatabase();
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var sql = @"
            SELECT e.* FROM Events e
            WHERE e.IsHighDemand = 1";

        var dbContext = scope.ServiceProvider
            .GetRequiredService<Infrastructure.Persistence.ApplicationDbContext>();

        var highDemandEvents = await dbContext.Events
            .FromSqlRaw(sql)
            .Include(e => e.TicketTypes)
            .ToListAsync(ct);

        if (highDemandEvents.Count == 0) return;

        bool anyCompensated = false;

        foreach (var @event in highDemandEvents)
        {
            foreach (var ticketType in @event.TicketTypes)
            {
                var key = InventoryKeyPrefix + ticketType.Id.Value;

                var redisValue = await db.StringGetAsync(key);

                if (!redisValue.HasValue)
                    continue;

                if (!long.TryParse(redisValue, out var redisRemaining))
                    continue;

                // Cross-reference the Redis counter against the actual SQL
                // AvailableCount, not just zero. If Redis was seeded with a
                // stale higher number (e.g., 100) but SQL dropped to 95 due
                // to concurrent successful requests that bypassed Redis,
                // redisRemaining (e.g., 97) would be > 95 — meaning
                // 2 more tickets were "sold" via Redis than SQL can fulfill.
                // Conversely, if redisRemaining < 0, the counter went
                // negative (classic oversell). Both cases need compensation.
                //
                // Skip ONLY if Redis <= SQL (in sync — Redis has fewer or
                // equal remaining tickets than SQL, meaning no oversell).
                // If Redis > SQL, oversell occurred (Redis allowed bookings
                // that SQL can't fulfill).
                var sqlAvailableCount = ticketType.AvailableCount;

                if (redisRemaining >= 0 && redisRemaining <= sqlAvailableCount)
                    continue;

                // Oversell detected — compute how many bookings to cancel.
                // If redisRemaining < 0: oversold by |redisRemaining|.
                // If redisRemaining >= sqlAvailableCount (but >= 0): the
                //   Redis counter is higher than what SQL can fulfill, meaning
                //   (redisRemaining - sqlAvailableCount) extra tickets were
                //   reserved via Redis but SQL doesn't have the inventory.
                var oversoldCount = redisRemaining < 0
                    ? (int)Math.Abs(redisRemaining)
                    : redisRemaining - sqlAvailableCount;

                _logger.LogWarning(
                    "Oversell detected: event {EventId}, ticket {TicketTypeId}, Redis remaining {RedisRemaining}, SQL available {SqlAvailable}, oversold by {OversoldCount}",
                    @event.Id.Value, ticketType.Id.Value, redisRemaining, sqlAvailableCount, oversoldCount);

                var latestBookings = await bookingRepo.GetLatestPendingByTicketTypeAsync(
                    ticketType.Id, (int)oversoldCount, ct);

                if (latestBookings.Count == 0)
                {
                    _logger.LogWarning(
                        "No pending bookings found to compensate for oversold ticket {TicketTypeId}",
                        ticketType.Id.Value);
                    continue;
                }

                foreach (var booking in latestBookings)
                {
                    var cancelResult = booking.Cancel(now, "Oversold — inventory reconciliation");

                    if (cancelResult.IsFailure)
                    {
                        _logger.LogWarning(
                            "Failed to cancel oversold booking {BookingId}: {Error}",
                            booking.Id.Value, cancelResult.Errors[0].Message);
                        continue;
                    }

                    @event.ReleaseSeats(ticketType.Id, booking.Quantity, now);

                    var compensationLog = new CompensationLogDto(
                        Id: Guid.NewGuid(),
                        BookingId: booking.Id.Value,
                        CompensationType: "CompensateOversoldBooking",
                        Payload: System.Text.Json.JsonSerializer.Serialize(new
                        {
                            BookingId = booking.Id.Value,
                            Reason = "Inventory reconciliation: oversold during strategy handover",
                            OccurredAt = now,
                            RedisRemaining = redisRemaining,
                        }),
                        OccurredOnUtc: now,
                        IdempotencyKey: $"compensation:CompensateOversoldBooking:{booking.Id.Value}",
                        ProcessedOnUtc: null,
                        Error: null,
                        RetryCount: 0,
                        NextRetryOnUtc: null);

                    compensationRepo.Add(compensationLog);

                    _logger.LogInformation(
                        "Cancelled oversold booking {BookingId} for ticket {TicketTypeId}",
                        booking.Id.Value, ticketType.Id.Value);

                    anyCompensated = true;
                }

                // Reset the Redis counter to the SQL AvailableCount — the
                // authoritative value after compensation. This resyncs the
                // counter so future reservations are accurate.
                await db.StringSetAsync(key, sqlAvailableCount);
            }
        }

        if (anyCompensated)
            await uow.CommitAsync(ct);
    }
}
