using Domain.Abstractions.Persistence;
using Domain.Aggregates.EventAggregate.Entities;
using Domain.Aggregates.EventAggregate.ValueObject;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

internal sealed class EventPhotoRepository : IEventPhotoRepository
{
    private readonly ApplicationDbContext _context;

    public EventPhotoRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<EventPhoto>> GetByEventIdAsync(EventId eventId, CancellationToken ct = default)
    {
        return await _context.EventPhotos
            .Where(p => p.EventId == eventId)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync(ct);
    }

    public async Task<EventPhoto?> GetByIdAsync(EventPhotoId id, CancellationToken ct = default)
    {
        return await _context.EventPhotos
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public void Add(EventPhoto photo)
    {
        _context.EventPhotos.Add(photo);
    }

    public void Update(EventPhoto photo)
    {
        _context.EventPhotos.Update(photo);
    }

    public void Delete(EventPhoto photo)
    {
        _context.EventPhotos.Remove(photo);
    }
}
