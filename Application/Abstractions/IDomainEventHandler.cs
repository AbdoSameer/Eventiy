using Domain.Primitives;

namespace Domain.Common
{
    public interface IDomainEventHandler<in TEvent>
        where TEvent : IDomainEvent
    {
        Result Handle(TEvent @event);
    }
}
