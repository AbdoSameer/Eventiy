using Application.Abstractions.Messaging;

namespace Application.Features.Bookings.Command.CancelBooking
{
    public sealed record CancelBookingCommand(
        Guid BookingId) : ICommand<bool>; 

}
