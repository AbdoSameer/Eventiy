using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Aggregates.UserAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.BookingAggregate.Events
{
    public class BookingExpiredEvent : DomainEvent, IDomainEvent
    {
        public override string Name => nameof(BookingExpiredEvent);
        public override string Domain => "Booking";

        public BookingId BookingId { get; }
        public UserId UserId { get; }
        public EventId EventId { get; }
        public TicketTypeId TicketTypeId { get; }
        public int Quantity { get; }

        public BookingExpiredEvent(
            BookingId bookingId,
            UserId userId,
            EventId eventId,
            TicketTypeId ticketTypeId,
            int quantity,
            DateTime occurredOnUtc,
            EventMetadata metadata) : base(occurredOnUtc, metadata)
        {
            BookingId = bookingId;
            UserId = userId;
            EventId = eventId;
            TicketTypeId = ticketTypeId;
            Quantity = quantity;
        }
    }
}