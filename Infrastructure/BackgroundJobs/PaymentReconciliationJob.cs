using Application.Abstractions.Payments;
using Domain.Abstractions.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundJobs;

public sealed class PaymentReconciliationJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PaymentReconciliationJob> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(2);
    private const int BatchSize = 50;

    public PaymentReconciliationJob(
        IServiceProvider serviceProvider,
        ILogger<PaymentReconciliationJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Payment Reconciliation Job started");

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Payment Reconciliation Job startup delay cancelled");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcileOrphanedPayments(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error reconciling orphaned payments");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("Payment Reconciliation Job stopped");
    }

    private async Task ReconcileOrphanedPayments(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var bookingRepo = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
        var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var orphanedBookings = await bookingRepo.GetPendingInstantBookingsPastHoldAsync(
            now, BatchSize, ct);

        if (orphanedBookings.Count == 0) return;

        _logger.LogInformation(
            "Reconciling {Count} orphaned Instant payment session(s)",
            orphanedBookings.Count);

        foreach (var booking in orphanedBookings)
        {
            try
            {
                var cancelResult = await paymentService.CancelPaymentAsync(
                    booking.Id.Value, ct);

                if (cancelResult.IsFailure)
                {
                    _logger.LogWarning(
                        "Failed to cancel payment for booking {BookingId}: {Errors}",
                        booking.Id.Value,
                        string.Join("; ", cancelResult.Errors.Select(e => e.Code)));
                }
                else
                {
                    _logger.LogInformation(
                        "Cancelled orphaned payment session for booking {BookingId}",
                        booking.Id.Value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error cancelling payment for booking {BookingId}",
                    booking.Id.Value);
            }
        }
    }
}
