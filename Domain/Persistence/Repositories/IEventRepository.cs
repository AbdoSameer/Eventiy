using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.ValueObject;

namespace Domain.Persistence.Repositories;

public interface IEventRepository
{
    Task AddEventAsync(
        Event @event,
        CancellationToken cancellationToken);

    Task<Event?> GetByIdAsync(
        EventId id,
        CancellationToken cancellationToken);

    void Update(Event @event);
}