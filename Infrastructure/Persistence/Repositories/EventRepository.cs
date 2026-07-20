using Domain.Abstractions.Persistence;
using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.ValueObject;
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

        /// <summary>
        /// Loads the Event and its TicketTypes with a pessimistic lock
        /// (UPDLOCK, HOLDLOCK) to prevent concurrent reservations from
        /// sneaking in during a high-demand toggle. The lock is held until
        /// the ambient transaction commits or rolls back.
        /// </summary>
        public async Task<Event?> GetByIdWithLockAsync(EventId id, CancellationToken cancellationToken)
        {
            var idValue = id.Value;

            var @event = await _applicationDbContext.Events
                .FromSqlInterpolated(
                    $"SELECT * FROM Events WITH (UPDLOCK, HOLDLOCK) WHERE Id = {idValue}")
                .Include(e => e.TicketTypes)
                .Include(e => e.Photos)
                .FirstOrDefaultAsync(cancellationToken);

            return @event;
        }
    }
}