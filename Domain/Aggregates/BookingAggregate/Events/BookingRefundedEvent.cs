using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Aggregates.UserAggregate.ValueObject;
using Domain.Common;
using Domain.Primitives;

namespace Domain.Aggregates.BookingAggregate.Events
{
    public class BookingRefundedEvent : DomainEvent, IDomainEvent
    {
        public override string Name => nameof(BookingRefundedEvent);
        public override string Domain => "Booking";
        public BookingId BookingId { get; }
        public UserId UserId { get; }
        public EventId EventId { get; }
        public Money RefundMoney { get; }
        public DateTime RefundedAt { get; }

        public BookingRefundedEvent(
            BookingId bookingId,
            UserId userId,
            EventId eventId,
            Money refundMoney,
            DateTime occurredOnUtc) : base(occurredOnUtc)

        {
            BookingId = bookingId;
            UserId = userId;
            EventId = eventId;
            RefundMoney = refundMoney;
            RefundedAt = occurredOnUtc;
        }
    }
}