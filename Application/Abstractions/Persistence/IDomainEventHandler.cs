using Domain.Common;
using Domain.Primitives;

namespace Application.Abstractions.Persistence
{
    public interface IDomainEventHandler<in TEvent>
        where TEvent : IDomainEvent
    {
        Task<Result> HandleAsync(TEvent @event);
    }
}
