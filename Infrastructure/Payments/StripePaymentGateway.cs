using Application.Abstractions.Payments;
using Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace Infrastructure.Payments;

public class StripePaymentGateway : IPaymentService
{
    private readonly StripeSettings _settings;
    private readonly ILogger<StripePaymentGateway> _logger;

    public StripePaymentGateway(
        IOptions<StripeSettings> settings,
        ILogger<StripePaymentGateway> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        StripeConfiguration.ApiKey = _settings.SecretKey;
    }

    public async Task<Result<PaymentInitiationResult>> InitiatePaymentAsync(
        Guid bookingId,
        string referenceCode,
        decimal amount,
        string currency,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        try
        {
            var unitAmount = (long)Math.Round(amount * 100);

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = ["card"],
                LineItems =
                [
                    new SessionLineItemOptions
                    {
                        Quantity = 1,
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = currency.ToLowerInvariant(),
                            UnitAmount = unitAmount,
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"Booking {referenceCode}",
                            },
                        },
                    },
                ],
                Mode = "payment",
                SuccessUrl = _settings.SuccessUrl,
                CancelUrl = _settings.CancelUrl,
                Metadata = new Dictionary<string, string>
                {
                    { "bookingId", bookingId.ToString() },
                },
            };

            var requestOptions = new RequestOptions
            {
                IdempotencyKey = idempotencyKey,
            };

            var service = new SessionService();
            var session = await service.CreateAsync(options, requestOptions, ct);

            _logger.LogInformation(
                "Created Stripe Checkout Session {SessionId} for booking {BookingId}, amount {Amount} {Currency}",
                session.Id, bookingId, amount, currency);

            return Result<PaymentInitiationResult>.Success(
                new PaymentInitiationResult(session.Url, null));
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex,
                "Stripe error creating Checkout Session for booking {BookingId}: {Message}",
                bookingId, ex.Message);

            return Result<PaymentInitiationResult>.Failure(
                Error.Failure("Payment.StripeError", ex.Message));
        }
    }

    public async Task<Result> CancelPaymentAsync(
        Guid bookingId,
        CancellationToken ct = default)
    {
        try
        {
            var service = new SessionService();
            var createdAfter = DateTime.UtcNow.AddDays(-1);
            var sessions = await service.ListAsync(new SessionListOptions
            {
                Limit = 100,
                Created = new Stripe.DateRangeOptions
                {
                    GreaterThan = createdAfter,
                },
            }, cancellationToken: ct);

            var session = sessions.FirstOrDefault(s =>
                s.Metadata.TryGetValue("bookingId", out var id) && id == bookingId.ToString());

            if (session is null || session.Status == "complete" || session.Status == "expired")
            {
                _logger.LogInformation(
                    "No active Stripe session found for booking {BookingId} — skipping cancel",
                    bookingId);
                return Result.Success();
            }

            var requestOptions = new RequestOptions
            {
                IdempotencyKey = $"expire-session-{session.Id}",
            };
            await service.ExpireAsync(session.Id, null, requestOptions, cancellationToken: ct);

            _logger.LogInformation(
                "Expired Stripe Checkout Session {SessionId} for booking {BookingId}",
                session.Id, bookingId);

            return Result.Success();
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex,
                "Stripe error cancelling session for booking {BookingId}: {Message}",
                bookingId, ex.Message);

            return Result.Failure(Error.Failure("Payment.StripeCancelError", ex.Message));
        }
    }
}
