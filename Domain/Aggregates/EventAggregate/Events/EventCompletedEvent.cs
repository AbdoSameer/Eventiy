using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.EventAggregate.Events
{
    public class EventCompletedEvent : DomainEvent, IDomainEvent
    {
        public EventId EventId { get; }
        public DateTime CompletedAt { get; }
        public override string Name => nameof(EventCompletedEvent);
        public override string Domain => "Event";

        public EventCompletedEvent(EventId eventId, DateTime occurredOnUtc, EventMetadata metadata) : base(occurredOnUtc, metadata)
        {
            EventId = eventId;
            CompletedAt = occurredOnUtc;
        }
    }
}