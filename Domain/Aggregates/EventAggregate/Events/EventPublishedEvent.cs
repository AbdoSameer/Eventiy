using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.EventAggregate.Events
{
    public class EventPublishedEvent : DomainEvent, IDomainEvent
    {
        public EventId EventId { get; }
        public DateTime PublishedAt { get; }
        public int TotalTicketTypes { get; }
        public override string Name => nameof(EventPublishedEvent);
        public override string Domain => "Event";

        public EventPublishedEvent(
            EventId eventId,
            int totalTicketTypes)
        {
            EventId = eventId;
            PublishedAt = DateTime.UtcNow;
            TotalTicketTypes = totalTicketTypes;
        }
    }
}