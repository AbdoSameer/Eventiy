using Domain.Aggregates.EventAggregate.Entities;
using Domain.Aggregates.EventAggregate.ValueObject;

namespace Domain.Abstractions.Persistence;

public interface IEventPhotoRepository
{
    Task<List<EventPhoto>> GetByEventIdAsync(EventId eventId, CancellationToken ct = default);

    Task<EventPhoto?> GetByIdAsync(EventPhotoId id, CancellationToken ct = default);

    void Add(EventPhoto photo);

    void Update(EventPhoto photo);

    void Delete(EventPhoto photo);
}
