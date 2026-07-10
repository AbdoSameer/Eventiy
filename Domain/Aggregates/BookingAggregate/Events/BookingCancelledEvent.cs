using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Aggregates.UserAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.BookingAggregate.Events
{
    public class BookingCancelledEvent : DomainEvent, IDomainEvent
    {
        public BookingId BookingId { get; }
        public UserId UserId { get; }
        public EventId EventId { get; }
        public TicketTypeId TicketTypeId { get; }   
        public int Quantity { get; }                 
        public string? Reason { get; }

        public override string Name => nameof(BookingCancelledEvent);
        public override string Domain => "Booking";

        public BookingCancelledEvent(
            BookingId bookingId,
            UserId userId,
            EventId eventId,
            TicketTypeId ticketTypeId,
            int quantity,                
            DateTime occurredOnUtc,
            string? reason = null) : base(occurredOnUtc)
        {
            BookingId = bookingId;  
            UserId = userId;
            EventId = eventId;
            TicketTypeId = ticketTypeId;   
            Quantity = quantity;            
            Reason = reason;
        }
    }
}