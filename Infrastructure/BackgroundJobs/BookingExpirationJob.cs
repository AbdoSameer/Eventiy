using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Abstractions.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundJobs;

public sealed class BookingExpirationJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BookingExpirationJob> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(1);
    private const int BatchSize = 100;

    public BookingExpirationJob(
        IServiceProvider serviceProvider,
        ILogger<BookingExpirationJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Booking Expiration Job started");

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Booking Expiration Job startup delay cancelled");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredBookings(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing expired bookings");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("Booking Expiration Job stopped");
    }

    private async Task ProcessExpiredBookings(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var bookingRepo = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
        var eventRepo = scope.ServiceProvider.GetRequiredService<IEventRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expiredBookings = await bookingRepo.GetExpiredPendingBookingsAsync(now, BatchSize, ct);

        if (expiredBookings.Count == 0) return;

        _logger.LogInformation("Expiring {Count} pending bookings", expiredBookings.Count);

        foreach (var booking in expiredBookings)
        {
            var expireResult = booking.Expire(now);
            if (expireResult.IsFailure)
            {
                _logger.LogWarning(
                    "Failed to expire booking {BookingId}: {Errors}",
                    booking.Id.Value,
                    string.Join("; ", expireResult.Errors.Select(e => e.Code)));
                continue;
            }

            var evt = await eventRepo.GetByIdAsync(booking.EventId, ct);
            if (evt is not null)
            {
                var releaseResult = evt.ReleaseSeats(
                    booking.TicketTypeId,
                    booking.Quantity,
                    now);

                if (releaseResult.IsFailure)
                {
                    _logger.LogWarning(
                        "Failed to release seats for expired booking {BookingId}: {Errors}",
                        booking.Id.Value,
                        string.Join("; ", releaseResult.Errors.Select(e => e.Code)));
                }
                else
                {
                    uow.EnforceFencingToken(evt, evt.RowVersion);
                }
            }
        }

        var rowsAffected = await uow.CommitAsync(ct);
        _logger.LogInformation("Expired {Count} bookings ({Rows} rows affected)",
            expiredBookings.Count, rowsAffected);
    }
}