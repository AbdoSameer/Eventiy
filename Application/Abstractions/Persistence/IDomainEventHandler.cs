using Domain.Common;

namespace Domain.Common;

public interface IDomainEventHandler<TEvent> where TEvent : IDomainEvent
{
    Task<Result> HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}