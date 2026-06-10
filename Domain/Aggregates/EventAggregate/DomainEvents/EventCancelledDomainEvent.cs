using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.EventAggregate.DomainEvents
{
    public sealed record EventCancelledDomainEvent(EventId EventId) : IDomainEvent;

}
