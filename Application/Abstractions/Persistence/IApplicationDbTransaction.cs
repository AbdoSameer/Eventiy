namespace Application.Abstractions.Persistence;

/// <summary>
/// Abstraction over an ambient database transaction. Callers must
/// <see cref="CommitAsync"/> or <see cref="RollbackAsync"/> before
/// disposing. Used by the ToggleHighDemandCommandHandler to wrap a
/// pessimistic lock (UPDLOCK, HOLDLOCK) + Redis seeding in a single
/// atomic database transaction.
/// </summary>
public interface IApplicationDbTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
