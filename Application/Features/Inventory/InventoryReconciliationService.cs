using Application.Abstractions.Inventory;
using Application.Abstractions.Payments;
using Application.Abstractions.Persistence;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate.Enums;
using Domain.Common;
using Microsoft.Extensions.Logging;

namespace Application.Features.Inventory;

public sealed class InventoryReconciliationService : IInventoryReconciliationService
{
    private readonly IEventRepository _eventRepo;
    private readonly IBookingRepository _bookingRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICompensationLogRepository _compensationRepo;
    private readonly IInventoryCounterStore _counterStore;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<InventoryReconciliationService> _logger;

    private const string InventoryKeyPrefix = "inv:ticket:";

    public InventoryReconciliationService(
        IEventRepository eventRepo,
        IBookingRepository bookingRepo,
        IUnitOfWork uow,
        ICompensationLogRepository compensationRepo,
        IInventoryCounterStore counterStore,
        TimeProvider timeProvider,
        ILogger<InventoryReconciliationService> logger)
    {
        _eventRepo = eventRepo;
        _bookingRepo = bookingRepo;
        _uow = uow;
        _compensationRepo = compensationRepo;
        _counterStore = counterStore;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result> ReconcileAsync(CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var highDemandEvents = await _eventRepo.GetHighDemandEventsAsync(ct);

        if (highDemandEvents.Count == 0)
            return Result.Success();

        bool anyCompensated = false;
        var redisResets = new List<(string Key, long Value)>();

        foreach (var @event in highDemandEvents)
        {
            foreach (var ticketType in @event.TicketTypes)
            {
                var key = InventoryKeyPrefix + ticketType.Id.Value;

                var redisRemaining = await _counterStore.GetRemainingAsync(ticketType.Id.Value.ToString(), ct);

                if (redisRemaining is null)
                    continue;

                var sqlAvailableCount = ticketType.AvailableCount;

                if (redisRemaining >= 0 && redisRemaining <= sqlAvailableCount)
                    continue;

                var oversoldCount = redisRemaining < 0
                    ? (int)Math.Abs(redisRemaining.Value)
                    : (int)(redisRemaining.Value - sqlAvailableCount);

                _logger.LogWarning(
                    "Oversell detected: event {EventId}, ticket {TicketTypeId}, Redis remaining {RedisRemaining}, SQL available {SqlAvailable}, oversold by {OversoldCount}",
                    @event.Id.Value, ticketType.Id.Value, redisRemaining, sqlAvailableCount, oversoldCount);

                var latestBookings = await _bookingRepo.GetLatestPendingByTicketTypeAsync(
                    ticketType.Id, oversoldCount, ct);

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

                    _compensationRepo.Add(compensationLog);

                    _logger.LogInformation(
                        "Cancelled oversold booking {BookingId} for ticket {TicketTypeId}",
                        booking.Id.Value, ticketType.Id.Value);

                    anyCompensated = true;
                }

                redisResets.Add((key, sqlAvailableCount));
            }
        }

        if (anyCompensated)
        {
            await _uow.CommitAsync(ct);

            foreach (var (key, value) in redisResets)
            {
                await _counterStore.SetRemainingAsync(key, value, ct);
            }
        }

        return Result.Success();
    }
}
