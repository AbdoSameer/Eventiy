using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.EventAggregate.Events
{
    public class EventDescriptionUpdatedEvent : DomainEvent, IDomainEvent
    {
        public override string Name => nameof(EventDescriptionUpdatedEvent);
        public override string Domain => "Event";

        public EventId EventId { get; }
        public string OldDescription { get; }
        public string NewDescription { get; }
        public DateTime UpdatedAt { get; }

        public EventDescriptionUpdatedEvent(
            EventId eventId,
            string oldDescription,
            string newDescription,
            DateTime occurredOnUtc) : base(occurredOnUtc)
        {
            EventId = eventId;
            OldDescription = oldDescription;
            NewDescription = newDescription;
            UpdatedAt = occurredOnUtc;
        }
    }
}
