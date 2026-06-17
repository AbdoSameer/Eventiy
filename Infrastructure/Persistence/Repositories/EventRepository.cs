using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;
using Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class EventRepository : IEventRepository 
    {
        private readonly ApplicationDbContext _context;

        public EventRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Result<Event?>> GetByIdAsync(
            EventId eventId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var eventAggregate = await _context.Events
                    .Include(eventItem => eventItem.TicketTypes)
                    .FirstOrDefaultAsync(eventItem => eventItem.Id == eventId, cancellationToken);

                return Result<Event?>.Success(eventAggregate);
            }
            catch (Exception exception)
            {
                return Result<Event?>.Failure($"Failed to load event {eventId.Value}: {exception.Message}");
            }
        }

        public async Task<Result<IReadOnlyCollection<Event>>> ListAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var events = await _context.Events
                    .Include(eventItem => eventItem.TicketTypes)
                    .ToListAsync(cancellationToken);

                return Result<IReadOnlyCollection<Event>>.Success(events);
            }
            catch (Exception exception)
            {
                return Result<IReadOnlyCollection<Event>>.Failure(
                    $"Failed to list events: {exception.Message}");
            }
        }

        public async Task<Result<bool>> ExistsAsync(
            EventId eventId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var exists = await _context.Events
                    .AnyAsync(eventItem => eventItem.Id == eventId, cancellationToken);

                return Result<bool>.Success(exists);
            }
            catch (Exception exception)
            {
                return Result<bool>.Failure($"Failed to check event {eventId.Value}: {exception.Message}");
            }
        }

        public async Task<Result> AddAsync(
            Event eventAggregate,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await _context.Events.AddAsync(eventAggregate, cancellationToken);

                return Result.Success();
            }
            catch (Exception exception)
            {
                return Result.Failure($"Failed to add event {eventAggregate.Id.Value}: {exception.Message}");
            }
        }

        public Result Update(Event eventAggregate)
        {
            try
            {
                _context.Events.Update(eventAggregate);

                return Result.Success();
            }
            catch (Exception exception)
            {
                return Result.Failure($"Failed to update event {eventAggregate.Id.Value}: {exception.Message}");
            }
        }

        public async Task<Result> DeleteAsync(
            EventId eventId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var eventAggregateResult = await GetByIdAsync(eventId, cancellationToken);

                if (eventAggregateResult.IsFailure)
                {
                    return Result.Failure(eventAggregateResult.Error);
                }

                if (eventAggregateResult.Value is null)
                {
                    return Result.Failure($"Event with ID {eventId.Value} was not found.");
                }

                _context.Events.Remove(eventAggregateResult.Value);

                return Result.Success();
            }
            catch (Exception exception)
            {
                return Result.Failure($"Failed to delete event {eventId.Value}: {exception.Message}");
            }
        }
    }
}
