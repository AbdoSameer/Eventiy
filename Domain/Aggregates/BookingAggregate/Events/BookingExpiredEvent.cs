using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.ValueObject;
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
        public DateTime ExpiredAt { get; }

        public BookingExpiredEvent(
            BookingId bookingId,
            UserId userId,
            EventId eventId)
        {
            BookingId = bookingId;
            UserId = userId;
            EventId = eventId;
            ExpiredAt = DateTime.UtcNow;
        }
    }
}