using Domain.Primitives;

namespace Domain.Common
{
    public interface IEventValidator<in TEvent>
        where TEvent : IDomainEvent
    {
        Result Validate(TEvent @event);
    }
}
