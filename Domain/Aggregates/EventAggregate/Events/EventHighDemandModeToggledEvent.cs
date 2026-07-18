using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.EventAggregate.Events
{
    public class EventHighDemandModeToggledEvent : DomainEvent, IDomainEvent
    {
        public EventId EventId { get; }
        public bool IsHighDemand { get; }

        public override string Name => nameof(EventHighDemandModeToggledEvent);
        public override string Domain => "Event";

        public EventHighDemandModeToggledEvent(
            EventId eventId,
            bool isHighDemand,
            DateTime occurredOnUtc) : base(occurredOnUtc)
        {
            EventId = eventId;
            IsHighDemand = isHighDemand;
        }
    }
}
