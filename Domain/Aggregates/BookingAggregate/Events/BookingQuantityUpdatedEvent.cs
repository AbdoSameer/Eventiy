using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.BookingAggregate.Events
{
    public class BookingQuantityUpdatedEvent : DomainEvent, IDomainEvent
    {
        public override string Name => nameof(BookingQuantityUpdatedEvent);
        public override string Domain => "Booking";

        public BookingId BookingId { get; }
        public decimal OldTotalAmount { get; }
        public decimal NewTotalAmount { get; }
        public DateTime UpdatedAt { get; }

        public BookingQuantityUpdatedEvent(
            BookingId bookingId,
            decimal oldTotalAmount,
            decimal newTotalAmount,
            IDateTimeProvider dateTimeProvider,
            EventMetadata metadata) : base(dateTimeProvider, metadata)

        {
            BookingId = bookingId;
            OldTotalAmount = oldTotalAmount;
            NewTotalAmount = newTotalAmount;
            UpdatedAt = dateTimeProvider.UtcNow;
        }
    }
}