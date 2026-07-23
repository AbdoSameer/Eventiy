using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.EventAggregate.Events
{
    public class EventDateUpdatedEvent : DomainEvent, IDomainEvent
    {
        public override string Name => nameof(EventDateUpdatedEvent);
        public override string Domain => "Event";

        public EventId EventId { get; }
        public DateTime OldDate { get; }
        public DateTime NewDate { get; }
        public DateTime UpdatedAt { get; }

        public EventDateUpdatedEvent(
            EventId eventId,
            DateTime oldDate,
            DateTime newDate,
            DateTime occurredOnUtc) : base(occurredOnUtc)
        {
            EventId = eventId;
            OldDate = oldDate;
            NewDate = newDate;
            UpdatedAt = occurredOnUtc;
        }
    }
}
