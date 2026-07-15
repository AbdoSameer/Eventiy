using Application.Abstractions.Payments;
using Domain.Common;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Payments;

public class MockPaymentGateway : IPaymentService
{
    private readonly ILogger<MockPaymentGateway> _logger;

    public MockPaymentGateway(ILogger<MockPaymentGateway> logger)
    {
        _logger = logger;
    }

    public Task<Result<PaymentInitiationResult>> InitiatePaymentAsync(
        Guid bookingId,
        string referenceCode,
        decimal amount,
        string currency,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Mock payment: booking {BookingId}, amount {Amount} {Currency} — no real charge",
            bookingId, amount, currency);

        return Task.FromResult(Result<PaymentInitiationResult>.Success(
            new PaymentInitiationResult(
                $"mock://payment/{bookingId}?amount={amount}&currency={currency}",
                null)));
    }

    public Task<Result> CancelPaymentAsync(Guid bookingId, CancellationToken ct = default)
    {
        _logger.LogInformation("Mock cancel: booking {BookingId} — no-op", bookingId);
        return Task.FromResult(Result.Success());
    }
}
