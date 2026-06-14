using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Repositories
{
    public interface IEventRepository
    {
        Task<Result<Event?>> GetByIdAsync(EventId eventId, CancellationToken cancellationToken = default);
        Task<Result<IReadOnlyCollection<Event>>> ListAsync(CancellationToken cancellationToken = default);
        Task<Result<bool>> ExistsAsync(EventId eventId, CancellationToken cancellationToken = default);
        Task<Result> AddAsync(Event eventAggregate, CancellationToken cancellationToken = default);
        Result Update(Event eventAggregate);
        Task<Result> DeleteAsync(EventId eventId, CancellationToken cancellationToken = default);
    }
}
