namespace Application.Abstractions.Persistence;

public interface IIdempotencyStore
{
    Task<bool> IsProcessedAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    Task MarkAsProcessedAsync(
        string idempotencyKey,
        DateTime processedAt,
        CancellationToken cancellationToken = default);
}