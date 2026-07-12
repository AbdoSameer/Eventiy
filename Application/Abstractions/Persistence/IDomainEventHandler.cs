using Domain.Common;

namespace Application.Abstractions.Persistence;

public interface IDomainEventHandler<TEvent> where TEvent : IDomainEvent
{
    Task<Result> HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}