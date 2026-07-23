using Application.Features.Bookings.Command.ConfirmBookingFromWebhook;
using Infrastructure.Payments;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace Eventy.WebApi.Controllers;

[ApiController]
[Route("api/webhooks/stripe")]
public class StripeWebhookController : ControllerBase
{
    private readonly ISender _sender;
    private readonly StripeSettings _settings;
    private readonly ILogger<StripeWebhookController> _logger;

    public StripeWebhookController(
        ISender sender,
        IOptions<StripeSettings> settings,
        ILogger<StripeWebhookController> logger)
    {
        _sender = sender;
        _settings = settings.Value;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> HandleWebhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                json,
                Request.Headers["Stripe-Signature"],
                _settings.WebhookSecret,
                throwOnApiVersionMismatch: false);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning("Stripe webhook signature verification failed: {Message}", ex.Message);
            return BadRequest("Invalid signature");
        }

        if (stripeEvent.Type != EventTypes.CheckoutSessionCompleted)
        {
            _logger.LogDebug("Unhandled Stripe event type: {EventType}", stripeEvent.Type);
            return Accepted();
        }

        return await HandleCheckoutSessionCompleted(stripeEvent);
    }

    private async Task<IActionResult> HandleCheckoutSessionCompleted(Event stripeEvent)
    {
        var session = stripeEvent.Data.Object as Session;
        if (session is null)
        {
            _logger.LogWarning("CheckoutSessionCompleted event has null session object");
            return Accepted();
        }

        if (!session.Metadata.TryGetValue("bookingId", out var bookingIdStr) ||
            !Guid.TryParse(bookingIdStr, out var bookingId))
        {
            _logger.LogWarning(
                "CheckoutSessionCompleted session {SessionId} has no valid bookingId metadata",
                session.Id);
            return Accepted();
        }

        if (session.PaymentStatus != "paid")
        {
            _logger.LogWarning(
                "CheckoutSessionCompleted session {SessionId} for booking {BookingId} is not paid (status: {Status})",
                session.Id, bookingId, session.PaymentStatus);
            return Accepted();
        }

        _logger.LogInformation(
            "Stripe Checkout Session {SessionId} completed for booking {BookingId} — confirming",
            session.Id, bookingId);

        var result = await _sender.Send(
            new ConfirmBookingFromWebhookCommand(bookingId, stripeEvent.Id));

        if (result.IsFailure)
        {
            _logger.LogError(
                "Failed to confirm booking {BookingId} from Stripe webhook: {Errors}",
                bookingId,
                string.Join("; ", result.Errors.Select(e => e.Code)));
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = $"Failed to confirm booking {bookingId} from Stripe webhook.",
                Status = StatusCodes.Status500InternalServerError,
                Extensions =
                {
                    ["errors"] = result.Errors.Select(e => new { e.Code, e.Message, e.Type })
                }
            });
        }

        return Ok();
    }
}
