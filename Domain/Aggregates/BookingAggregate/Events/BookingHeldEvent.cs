using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Aggregates.UserAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.BookingAggregate.Events
{
    public class BookingHeldEvent : DomainEvent, IDomainEvent
    {
        public override string Name => nameof(BookingHeldEvent);
        public override string Domain => "Booking";

        public BookingId BookingId { get; }
        public UserId UserId { get; }
        public EventId EventId { get; }
        public TicketTypeId TicketTypeId { get; }
        public int Quantity { get; }
        public DateTime HoldExpiresAt { get; }

        public BookingHeldEvent(
            BookingId bookingId,
            UserId userId,
            EventId eventId,
            TicketTypeId ticketTypeId,
            int quantity,
            DateTime holdExpiresAt,
            DateTime occurredOnUtc) : base(occurredOnUtc)
        {
            BookingId = bookingId;
            UserId = userId;
            EventId = eventId;
            TicketTypeId = ticketTypeId;
            Quantity = quantity;
            HoldExpiresAt = holdExpiresAt;
        }
    }
}