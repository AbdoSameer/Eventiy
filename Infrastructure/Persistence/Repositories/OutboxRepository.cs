using Application.Abstractions.Outbox; 
using Domain.Common;
using Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public sealed class OutboxRepository : IOutboxRepository
{
    private readonly ApplicationDbContext _context;
    private readonly IEventSerializer _serializer;

    public OutboxRepository(ApplicationDbContext context, IEventSerializer serializer)
    {
        _context = context;
        _serializer = serializer;
    }

    public async Task AddAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        // 1. Serialize
        var payload = _serializer.Serialize(domainEvent);

        // 2. Create Infrastructure Entity
        var outboxMessage = new OutboxMessage(domainEvent, payload);

        // 3. Save
        await _context.OutboxMessages.AddAsync(outboxMessage, cancellationToken);
    }

    public async Task<List<IDomainEvent>> GetUnprocessedEventsAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        // 1. Get Infrastructure Entities
        var messages = await _context.OutboxMessages
            .Where(m => !m.IsProcessed && m.IsReadyForProcessing)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        // 2. Convert to Domain Events
        var domainEvents = new List<IDomainEvent>();
        foreach (var message in messages)
        {
            var domainEvent = _serializer.Deserialize(message.EventName, message.Payload);
            domainEvents.Add(domainEvent);
        }

        return domainEvents;
    }

    public async Task MarkAsProcessedAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var message = await _context.OutboxMessages
            .FirstOrDefaultAsync(m => m.Id == eventId, cancellationToken);

        if (message != null)
        {
            message.MarkAsProcessed();
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkAsFailedAsync(Guid eventId, string error, CancellationToken cancellationToken = default)
    {
        var message = await _context.OutboxMessages
            .FirstOrDefaultAsync(m => m.Id == eventId, cancellationToken);

        if (message != null)
        {
            message.MarkAsFailed(error);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task AddRangeAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        var outboxMessages = domainEvents.Select(domainEvent =>
        {
            var payload = _serializer.Serialize(domainEvent);
            return new OutboxMessage(domainEvent, payload);
        }).ToList();

        if (outboxMessages.Any())
        {
            await _context.OutboxMessages.AddRangeAsync(outboxMessages, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}