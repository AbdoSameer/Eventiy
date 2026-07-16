using Application.Abstractions.Outbox;
using Application.Abstractions.Persistence;
using Domain.Common;
using Microsoft.EntityFrameworkCore;
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

        return sqlEx.Number == uniqueConstraintViolation
            || sqlEx.Number == uniqueKeyViolation;
    }

    private void DetachAllTrackedEntities()
    {
        foreach (var entry in _context.ChangeTracker.Entries().ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
}
