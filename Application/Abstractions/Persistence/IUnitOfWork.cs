using Domain.Common;

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

    /// <summary>
    /// Forces EF Core to track the given aggregate root as Modified
    /// and stamps the <paramref name="rowVersion"/> as the concurrency
    /// token's OriginalValue. This guarantees EF Core emits
    ///   UPDATE ... WHERE Id = @id AND RowVersion = @rowVersion
    /// during the next <see cref="CommitAsync"/> call — even if no
    /// scalar property on the aggregate root changed (only child
    /// entities like TicketType were mutated).
    ///
    /// Without this call, EF Core's change tracker does not include
    /// the aggregate root in the UPDATE batch when only child entities
    /// changed, making the RowVersion fencing token a dead code path.
    /// </summary>
    void EnforceFencingToken(
        IAggregateRoot aggregateRoot,
        byte[] rowVersion,
        string concurrencyTokenPropertyName = "RowVersion");
}