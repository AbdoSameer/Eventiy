using Domain.Common;

namespace Application.Abstractions.Outbox;


public interface IOutboxRepository
{
    Task AddAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
    Task<List<IDomainEvent>> GetUnprocessedEventsAsync(int batchSize, CancellationToken cancellationToken = default);
    Task MarkAsProcessedAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task MarkAsFailedAsync(Guid eventId, string error, CancellationToken cancellationToken = default);
}