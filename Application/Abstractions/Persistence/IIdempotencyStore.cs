namespace Application.Abstractions.Persistence;

public interface IIdempotencyStore
{
    Task<bool> IsProcessedAsync(Guid eventId, CancellationToken cancellationToken = default);

    Task MarkAsProcessedAsync(
        Guid eventId,
        string idempotencyKey,
        DateTime processedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tracks the processed event in the current DbContext without calling SaveChanges.
    /// The caller must commit the unit of work to persist this record atomically with
    /// its own changes — ensuring a crash between commit and mark-as-processed is impossible.
    /// </summary>
    void MarkAsProcessed(
        Guid eventId,
        string idempotencyKey,
        DateTime processedAt);
}
