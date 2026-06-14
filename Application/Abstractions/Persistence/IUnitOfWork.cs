using Domain.Common;

namespace Application.Abstractions.Persistence
{
    public interface IUnitOfWork
    {
        Task<Result> CommitAsync(CancellationToken cancellationToken = default);
    }
}
