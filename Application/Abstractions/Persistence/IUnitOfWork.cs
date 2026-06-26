namespace Application.Abstractions.Persistence;

public interface IUnitOfWork
{
    /// <summary>
    /// ✅ Atomic Commit: Saves domain changes + outbox messages
    /// ❌ Does NOT publish events immediately
    /// </summary>
    Task<int> CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// ✅ Save changes without event extraction
    /// Used by background processors
    /// </summary>
    Task<int> CommitWithoutEventsAsync(CancellationToken cancellationToken = default);
}