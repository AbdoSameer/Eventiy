using Application.Abstractions.Outbox;
using Application.Abstractions.Persistence;
using Domain.Common;
using Domain.Primitives;
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

    /// <summary>
    /// ✅ Atomic Commit: Domain Changes + Outbox Messages in ONE Transaction
    /// ❌ NO MediatR Publishing Here! OutboxProcessor handles that.
    /// </summary>
    public async Task<int> CommitAsync(CancellationToken cancellationToken = default)
    {
        // 1️⃣ Extract Domain Events
        var domainEvents = ExtractDomainEvents();

        // 2️⃣ Stage Outbox Messages في Change Tracker (لا I/O هنا)
        if (domainEvents.Count > 0)
            _outboxService.AddFromDomainEvents(domainEvents);  // ✅ sync، لا await

        // 3️⃣ ONE atomic SaveChanges: Domain changes + Outbox messages معاً
        var result = await _context.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation(
            "✅ Committed {Changes} changes with {Events} outbox events",
            result, domainEvents.Count);

        return result;
    }

    /// <summary>
    /// ✅ Save changes WITHOUT extracting/publishing events
    /// Used by OutboxProcessor to save processed status
    /// </summary>
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
}