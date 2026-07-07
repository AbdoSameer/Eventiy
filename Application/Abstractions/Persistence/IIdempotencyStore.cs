namespace Application.Abstractions.Persistence;

public interface IIdempotencyStore
{
    Task<bool> IsProcessedAsync(Guid eventId, CancellationToken cancellationToken = default);

    Task MarkAsProcessedAsync(
        Guid eventId,
        string idempotencyKey,
        DateTime processedAt,
        CancellationToken cancellationToken = default);
}
