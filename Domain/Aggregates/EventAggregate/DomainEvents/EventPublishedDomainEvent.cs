using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;


namespace Domain.Aggregates.EventAggregate.DomainEvents

{
    public sealed record EventPublishedDomainEvent(EventId EventId) : IDomainEvent
    {
        public string Name => nameof(EventPublishedDomainEvent);
        public string Domain => "Event";
    }
}