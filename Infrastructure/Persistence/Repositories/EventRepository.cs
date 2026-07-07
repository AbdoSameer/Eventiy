using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public sealed class EventRepository : IEventRepository
    {
        private readonly ApplicationDbContext _applicationDbContext;

        public EventRepository(ApplicationDbContext applicationDbContext)
        {
            _applicationDbContext = applicationDbContext;
        }

        public async Task AddEventAsync(Event @event, CancellationToken cancellationToken)
        {
            await _applicationDbContext.Events.AddAsync(@event, cancellationToken);
        }

        public async Task<Event?> GetByIdAsync(EventId id, CancellationToken cancellationToken)
        {
            return await _applicationDbContext.Events
                .Include(e => e.TicketTypes)
                .Include(e => e.Photos)
                .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        }

        public void Update(Event @event)
        {

        }
    }
}