using Application.Abstractions.Persistence;
using Domain.Common;

namespace Infrastructure.Persistence
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Result> CommitAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _context.SaveChangesAsync(cancellationToken);

                return Result.Success();
            }
            catch (Exception exception)
            {
                return Result.Failure($"Failed to save changes: {exception.Message}");
            }
        }
    }
}
