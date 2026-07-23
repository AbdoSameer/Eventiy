using Application.Abstractions.Outbox;
using Application.Abstractions.Persistence;
using Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence.Repositories;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private readonly IOutboxMessageService _outboxService;
    private readonly ILogger<UnitOfWork> _logger;

    public UnitOfWork(
        ApplicationDbContext context,
        IOutboxMessageService outboxService,
        ILogger<UnitOfWork> logger = null)
    {
        _context = context;
        _outboxService = outboxService;
        _logger = logger;
    }

    public async Task<int> CommitAsync(CancellationToken cancellationToken = default)
    {
        var domainEvents = ExtractDomainEvents();

        if (domainEvents.Count > 0)
            _outboxService.AddFromDomainEvents(domainEvents);

        try
        {
            var result = await _context.SaveChangesAsync(cancellationToken);

            _logger?.LogInformation(
                "Committed {Changes} changes with {Events} outbox events",
                result, domainEvents.Count);

            return result;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException(
                "A concurrency conflict occurred while saving changes. The data was modified by another process.", ex);
        }
        catch (DbUpdateException ex) when (IsDuplicateIdempotencyKeyError(ex))
        {
            _logger?.LogWarning(
                ex,
                "Duplicate IdempotencyKey detected — outbox messages were already staged by a prior transaction. Ignoring.");

            DetachAllTrackedEntities();

            return 0;
        }
    }

    public async Task<int> CommitWithoutEventsAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);

    public void EnforceFencingToken(
        IAggregateRoot aggregateRoot,
        byte[] rowVersion,
        string concurrencyTokenPropertyName = "RowVersion")
    {
        var entry = _context.Entry(aggregateRoot);

        // If the entity is detached (loaded via a different DbContext scope),
        // attach it so the change tracker includes it in the next SaveChanges.
        if (entry.State == EntityState.Detached)
            entry.State = EntityState.Unchanged;

        // Stamp the original RowVersion so EF Core emits
        //   UPDATE ... WHERE Id = @id AND RowVersion = @rowVersion
        entry.Property(concurrencyTokenPropertyName).OriginalValue = rowVersion;

        // Mark as Modified so EF Core includes the aggregate root in the
        // UPDATE batch even if no scalar property on it changed (only child
        // entities like TicketType were mutated by ReserveSeats).
        entry.State = EntityState.Modified;
    }

    public async Task ExecuteInTransactionAsync(
        Func<IApplicationDbTransaction, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _context.Database
                .BeginTransactionAsync(cancellationToken);

            var appTx = new EfDbTransaction(tx);
            try
            {
                await operation(appTx, cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private List<IDomainEvent> ExtractDomainEvents()
    {
        var aggregates = _context.ChangeTracker
            .Entries<IAggregateRoot>()
            .Select(e => e.Entity)
            .ToList();

        var events = aggregates
            .SelectMany(a => a.DomainEvents ?? Enumerable.Empty<IDomainEvent>())
            .ToList();

        aggregates.ForEach(a => a.ClearDomainEvents());

        return events;
    }

    private static bool IsDuplicateIdempotencyKeyError(DbUpdateException ex)
    {
        if (ex.InnerException is not Microsoft.Data.SqlClient.SqlException sqlEx)
            return false;

        const int uniqueConstraintViolation = 2601;
        const int uniqueKeyViolation = 2627;

        return (sqlEx.Number == uniqueConstraintViolation || sqlEx.Number == uniqueKeyViolation)
            && sqlEx.Message.Contains("ProcessedEvents");
    }

    private void DetachAllTrackedEntities()
    {
        foreach (var entry in _context.ChangeTracker.Entries().ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    private sealed class EfDbTransaction : IApplicationDbTransaction
    {
        private readonly IDbContextTransaction _tx;

        public EfDbTransaction(IDbContextTransaction tx) => _tx = tx;

        public Task CommitAsync(CancellationToken cancellationToken = default)
            => _tx.CommitAsync(cancellationToken);

        public Task RollbackAsync(CancellationToken cancellationToken = default)
            => _tx.RollbackAsync(cancellationToken);

        public async ValueTask DisposeAsync() => await _tx.DisposeAsync();
    }
}
