using Domain.Abstractions.Persistence;
using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.ValueObject;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class EventRepository : IEventRepository
    {
        private readonly ApplicationDbContext _applicationDbContext;

        public EventRepository(ApplicationDbContext applicationDbContext)
        {
            _applicationDbContext = applicationDbContext;
        }

        public Task<Event> AddEventAsync(Event @event, CancellationToken cancellationToken)
        {
            var entityEntry = _applicationDbContext.Events.Add(@event);

            return Task.FromResult(entityEntry.Entity);

        }

        public async Task<Event?> GetByIdAsync(EventId id, CancellationToken ct = default)
        {
            return await _applicationDbContext.Events
                             .Include(e => e.TicketTypes)
                             .FirstOrDefaultAsync(e => e.Id == id, ct);

        } 
    }
    
}
