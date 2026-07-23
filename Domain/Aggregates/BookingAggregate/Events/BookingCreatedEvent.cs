using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Aggregates.UserAggregate.ValueObject;
using Domain.Common;
using Domain.Primitives;

namespace Domain.Aggregates.BookingAggregate.Events
{
    public class BookingCreatedEvent : DomainEvent, IDomainEvent
    {
        public override string Name => nameof(BookingCreatedEvent);
        public override string Domain => "Booking";

        public BookingId BookingId { get; }
        public UserId UserId { get; }
        public EventId EventId { get; }
        public TicketTypeId TicketTypeId { get; }  
        public int Quantity { get; }
        public Money Money { get; }

        [System.Text.Json.Serialization.JsonConstructor]
        public BookingCreatedEvent(
            BookingId bookingId,
            UserId userId,
            EventId eventId,
            TicketTypeId ticketTypeId, 
            int quantity,
            Money money,
            DateTime occurredOnUtc) : base(occurredOnUtc)
        {
            BookingId = bookingId;
            UserId = userId;
            EventId = eventId;
            TicketTypeId = ticketTypeId; 
            Quantity = quantity;
            Money = money;
        }
    }
}