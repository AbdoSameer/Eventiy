using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.ValueObject;

namespace Domain.Abstractions.Persistence;

public interface IEventRepository
{
    Task AddEventAsync(
        Event @event,
        CancellationToken cancellationToken);

    Task<Event?> GetByIdAsync(
        EventId id,
        CancellationToken cancellationToken);
}