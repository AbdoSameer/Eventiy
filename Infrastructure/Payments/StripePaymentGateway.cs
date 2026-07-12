using Application.Abstractions.Payments;
using Domain.Common;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Payments;

public class StripePaymentGateway : IPaymentService
{
    private readonly ILogger<StripePaymentGateway> _logger;

    public StripePaymentGateway(ILogger<StripePaymentGateway> logger)
    {
        _logger = logger;
    }

    public Task<Result<PaymentInitiationResult>> InitiatePaymentAsync(
        Guid bookingId,
        string referenceCode,
        decimal amount,
        string currency,
        CancellationToken ct = default)
    {
        // In production, call Stripe API to create a Checkout Session or PaymentIntent.
        // For development, return null — the frontend will simulate payment success.
        _logger.LogInformation(
            "Initiating payment for booking {BookingId}, amount {Amount} {Currency} (mock — no real Stripe call)",
            bookingId, amount, currency);

        return Task.FromResult(Result<PaymentInitiationResult>.Success(
            new PaymentInitiationResult(null, null)));
    }
}
