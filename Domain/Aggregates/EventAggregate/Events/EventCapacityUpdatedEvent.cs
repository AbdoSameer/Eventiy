using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.EventAggregate.Events
{
    public class EventCapacityUpdatedEvent : DomainEvent, IDomainEvent
    {
        public override string Name => nameof(EventCapacityUpdatedEvent);
        public override string Domain => "Event";

        public EventId EventId { get; }
        public int OldCapacity { get; }
        public int NewCapacity { get; }
        public DateTime UpdatedAt { get; }


        public EventCapacityUpdatedEvent(
            EventId eventId,
            int oldCapacity,
            int newCapacity,
            DateTime occurredOnUtc,
            EventMetadata metadata) : base(occurredOnUtc, metadata)
        {
            EventId = eventId;
            OldCapacity = oldCapacity;
            NewCapacity = newCapacity;
            UpdatedAt = occurredOnUtc;
        }
    }
}