using Application.Abstractions.Persistence;

namespace Infrastructure.Persistence
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<int> CommitAsync(CancellationToken cancellationToken = default)
        {
        
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
