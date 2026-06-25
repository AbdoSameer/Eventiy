using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.EventAggregate.Events
{
    public class EventCancelledEvent : DomainEvent, IDomainEvent
    {
        public EventId EventId { get; }
        public DateTime CancelledAt { get; }
        public string? Reason { get; }
        public override string Name => nameof(EventCancelledEvent);
        public override string Domain => "Event";

        public EventCancelledEvent(
            EventId eventId,
            string? reason = null)
        {
            EventId = eventId;
            CancelledAt = DateTime.UtcNow;
            Reason = reason;
        }
    }
}