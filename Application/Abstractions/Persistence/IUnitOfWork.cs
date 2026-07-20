namespace Application.Abstractions.Persistence;

public interface IUnitOfWork
{
    /// <summary>
    /// Atomic Commit: Saves domain changes + outbox messages
    /// Does NOT publish events immediately
    /// </summary>
    Task<int> CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Save changes without event extraction
    /// Used by background processors
    /// </summary>
    Task<int> CommitWithoutEventsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the given operation inside a database transaction with
    /// the configured execution strategy (retry-aware). Use for operations
    /// that require pessimistic locking (UPDLOCK, HOLDLOCK) and must
    /// commit atomically (e.g. the strategy handover toggle).
    /// </summary>
    Task ExecuteInTransactionAsync(
        Func<IApplicationDbTransaction, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);
}