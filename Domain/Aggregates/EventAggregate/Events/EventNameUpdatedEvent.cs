using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.EventAggregate.Events
{
    public class EventNameUpdatedEvent : DomainEvent, IDomainEvent
    {
        public override string Name => nameof(EventNameUpdatedEvent);
        public override string Domain => "Event";

        public EventId EventId { get; }
        public string OldName { get; }
        public string NewName { get; }
        public DateTime UpdatedAt { get; }

        public EventNameUpdatedEvent(
            EventId eventId,
            string oldName,
            string newName,
            DateTime occurredOnUtc) : base(occurredOnUtc)
        {
            EventId = eventId;
            OldName = oldName;
            NewName = newName;
            UpdatedAt = occurredOnUtc;
        }
    }
}
