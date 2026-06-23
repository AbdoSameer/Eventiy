using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.BookingAggregate.Events
{
    public class BookingCancelledEvent : DomainEvent, IDomainEvent
    {
        // ? Added the missing BookingId property
        public BookingId BookingId { get; }
        public UserId UserId { get; }
        public EventId EventId { get; }
        public string? Reason { get; }

        public override string Name => nameof(BookingCancelledEvent);
        public override string Domain => "Booking";

        public BookingCancelledEvent(
            BookingId bookingId,
            UserId userId,
            EventId eventId,
            string? reason = null)
        {
            BookingId = bookingId;  
            UserId = userId;
            EventId = eventId;
            Reason = reason;
        }
    }
}