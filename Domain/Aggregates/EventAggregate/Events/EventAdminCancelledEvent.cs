using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.EventAggregate.Events
{
    public class EventAdminCancelledEvent : DomainEvent, IDomainEvent
    {
        public EventId EventId { get; }
        public DateTime CancelledAt { get; }
        public string? Reason { get; }
        public override string Name => nameof(EventAdminCancelledEvent);
        public override string Domain => "Event";

        public EventAdminCancelledEvent(
            EventId eventId,
            DateTime occurredOnUtc,
            string? reason = null) : base(occurredOnUtc)
        {
            EventId = eventId;
            CancelledAt = occurredOnUtc;
            Reason = reason;
        }
    }
}
