using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.EventAggregate.Events
{
    public class EventLocationUpdatedEvent : DomainEvent, IDomainEvent
    {
        public override string Name => nameof(EventLocationUpdatedEvent);
        public override string Domain => "Event";

        public EventId EventId { get; }
        public string OldLocationSummary { get; }
        public string NewLocationSummary { get; }
        public DateTime UpdatedAt { get; }

        public EventLocationUpdatedEvent(
            EventId eventId,
            string oldLocationSummary,
            string newLocationSummary,
            DateTime occurredOnUtc) : base(occurredOnUtc)
        {
            EventId = eventId;
            OldLocationSummary = oldLocationSummary;
            NewLocationSummary = newLocationSummary;
            UpdatedAt = occurredOnUtc;
        }
    }
}
