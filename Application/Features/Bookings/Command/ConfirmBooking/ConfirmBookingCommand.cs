using Application.Abstractions.Messaging;
using Application.Abstractions.Security;

namespace Application.Features.Bookings.Command.ConfirmBooking
{
    public sealed record ConfirmBookingCommand(
        Guid BookingId) : ICommand<bool>, IAuthorizableRequest
    {
        public string[] RequiredRoles => ["Admin", "Organizer"];
    }
}
