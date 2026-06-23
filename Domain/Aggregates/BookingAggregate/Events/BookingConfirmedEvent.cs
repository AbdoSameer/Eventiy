using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.BookingAggregate.Events
{
    public class BookingConfirmedEvent : DomainEvent, IDomainEvent
    {
        public override string Name => nameof(BookingConfirmedEvent);
        public override string Domain => "Booking";

        public BookingId BookingId { get; }
        public UserId UserId { get; }
        public EventId EventId { get; }
        public DateTime ConfirmedAt { get; }

        public BookingConfirmedEvent(
            BookingId bookingId,
            UserId userId,
            EventId eventId)
        {
            BookingId = bookingId;
            UserId = userId;
            EventId = eventId;
            ConfirmedAt = DateTime.UtcNow;
        }
    }
}