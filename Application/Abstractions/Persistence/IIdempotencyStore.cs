namespace Application.Abstractions.Persistence;

public interface IIdempotencyStore
{
    Task<bool> IsProcessedAsync(Guid eventId, CancellationToken cancellationToken = default);

    void MarkAsProcessed(
        Guid eventId,
        string idempotencyKey,
        DateTime processedAt);
}
