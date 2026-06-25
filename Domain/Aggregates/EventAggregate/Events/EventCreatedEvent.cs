using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.EventAggregate.Events
{
    public class EventCreatedEvent : DomainEvent, IDomainEvent
    {
        public EventId EventId { get; }
        public string EventName { get; }
        public DateTime Date { get; }
        public int Capacity { get; }
        public override string Name => nameof(EventCreatedEvent);
        public override string Domain => "Event";


        public EventCreatedEvent(
            EventId eventId,
            string name,
            DateTime date,
            int capacity)
        {
            EventId = eventId;
            EventName = name;
            Date = date;
            Capacity = capacity;
        }
    }
}