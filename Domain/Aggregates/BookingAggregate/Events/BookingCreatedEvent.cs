using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.BookingAggregate.Events
{
    public class BookingCreatedEvent : DomainEvent, IDomainEvent
    {
        public override string Name => nameof(BookingCreatedEvent);
        public override string Domain => "Booking";
        public BookingId BookingId { get; }
        public UserId UserId { get; }
        public EventId EventId { get; }
        public int Quantity { get; }
        public decimal TotalAmount { get; }

        public BookingCreatedEvent(
            BookingId bookingId,
            UserId userId,
            EventId eventId,
            int quantity,
            decimal totalAmount,
            IDateTimeProvider dateTimeProvider,
            EventMetadata metadata) : base(dateTimeProvider, metadata)
        {
            BookingId = bookingId;
            UserId = userId;
            EventId = eventId;
            Quantity = quantity;
            TotalAmount = totalAmount;
        }
    }
}