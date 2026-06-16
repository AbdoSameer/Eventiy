using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.EventAggregate.DomainEvents
{
    public sealed record EventCancelledDomainEvent(EventId EventId) : IDomainEvent
    {
        public string Name => throw new NotImplementedException();

        public string Domain => throw new NotImplementedException();
    }
}
