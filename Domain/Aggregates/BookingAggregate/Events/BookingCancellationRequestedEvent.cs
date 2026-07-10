using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Aggregates.UserAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.BookingAggregate.Events
{
    public class BookingCancellationRequestedEvent : DomainEvent
    {
        public override string Name => nameof(BookingCancellationRequestedEvent);
        public override string Domain => "Booking";

        public BookingId BookingId { get; }
        public UserId UserId { get; }
        public EventId EventId { get; }
        public string? Reason { get; }

        public BookingCancellationRequestedEvent(
            BookingId bookingId,
            UserId userId,
            EventId eventId,
            DateTime occurredOnUtc,
            string? reason = null) : base(occurredOnUtc)
        {
            BookingId = bookingId;
            UserId = userId;
            EventId = eventId;
            Reason = reason;
        }
    }
}