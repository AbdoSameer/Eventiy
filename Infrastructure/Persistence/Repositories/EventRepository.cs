using Application.Abstractions.Persistence;
using Domain.Aggregates.EventAggregate;

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

    }
}
