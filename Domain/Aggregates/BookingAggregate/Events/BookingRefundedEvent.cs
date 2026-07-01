using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Aggregates.UserAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.BookingAggregate.Events
{
    public class BookingRefundedEvent : DomainEvent, IDomainEvent
    {
        public override string Name => nameof(BookingRefundedEvent);
        public override string Domain => "Booking";
        public BookingId BookingId { get; }
        public UserId UserId { get; }
        public EventId EventId { get; }
        public decimal RefundAmount { get; }
        public DateTime RefundedAt { get; }

        public BookingRefundedEvent(
            BookingId bookingId,
            UserId userId,
            EventId eventId,
            decimal refundAmount,
            DateTime occurredOnUtc,
            EventMetadata metadata) : base(occurredOnUtc, metadata)

        {
            BookingId = bookingId;
            UserId = userId;
            EventId = eventId;
            RefundAmount = refundAmount;
            RefundedAt = occurredOnUtc;
        }
    }
}