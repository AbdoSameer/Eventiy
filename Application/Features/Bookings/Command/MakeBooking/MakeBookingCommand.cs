using Application.Abstractions.Messaging;

namespace Application.Features.Bookings.Command.MakeBooking
{
    public sealed record MakeBookingCommand(
        
        Guid EventId,
        Guid UserId,
        int Quantity,
        decimal price,
        string Currency
        ) : ICommand<Guid>;
    
}
