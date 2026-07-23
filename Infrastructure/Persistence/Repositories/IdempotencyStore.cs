using Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

internal sealed class IdempotencyStore : IIdempotencyStore
{
    private readonly ApplicationDbContext _context;

    public IdempotencyStore(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> IsProcessedAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        return await _context.ProcessedEvents
            .AnyAsync(e => e.EventId == eventId, cancellationToken);
    }

    public void MarkAsProcessed(
        Guid eventId,
        string idempotencyKey,
        DateTime processedAt)
    {
        _context.ProcessedEvents.Add(new ProcessedEvent
        {
            EventId = eventId,
            IdempotencyKey = idempotencyKey,
            ProcessedAt = processedAt,
        });
    }
}
