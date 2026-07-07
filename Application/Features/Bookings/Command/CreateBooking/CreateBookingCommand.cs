using Application.Abstractions.Messaging;
using Domain.Aggregates.BookingAggregate.Enums;

namespace Application.Features.Bookings.Command.CreateBooking
{
    public sealed record CreateBookingCommand : ICommand<Guid>
    {
        public Guid EventId { get; init; }
        public Guid TicketTypeId { get; init; }
        public int Quantity { get; init; }
        public PaymentMethod PaymentMethod { get; init; } 
    }
}