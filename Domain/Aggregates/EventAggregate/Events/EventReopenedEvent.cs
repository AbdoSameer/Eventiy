using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.EventAggregate.Events
{
    public class EventReopenedEvent : DomainEvent, IDomainEvent
    {
        public EventId EventId { get; }
        public DateTime ReopenedAt { get; }
        public override string Name => nameof(EventReopenedEvent);
        public override string Domain => "Event";

        public EventReopenedEvent(EventId eventId, DateTime occurredOnUtc) : base(occurredOnUtc)
        {
            EventId = eventId;
            ReopenedAt = occurredOnUtc;
        }
    }
}
