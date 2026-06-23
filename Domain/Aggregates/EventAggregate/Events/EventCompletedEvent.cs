using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.EventAggregate.Events
{
    public class EventCompletedEvent : DomainEvent, IDomainEvent
    {
        public EventId EventId { get; }
        public DateTime CompletedAt { get; }
        public override string Name => nameof(EventCompletedEvent);
        public override string Domain => "Booking";

        public EventCompletedEvent(EventId eventId)
        {
            EventId = eventId;
            CompletedAt = DateTime.UtcNow;
        }
    }
}