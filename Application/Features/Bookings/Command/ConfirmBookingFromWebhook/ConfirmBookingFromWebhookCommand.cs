using Application.Abstractions.Messaging;

namespace Application.Features.Bookings.Command.ConfirmBookingFromWebhook;

public sealed record ConfirmBookingFromWebhookCommand(
    Guid BookingId,
    string StripeEventId) : ICommand<bool>;
