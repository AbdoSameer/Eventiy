using Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;
namespace Infrastructure.Persistence
{
    internal sealed class ReadDbContextAdapter : IApplicationReadDbContext
    {
        private readonly ApplicationDbContext _context;
        public ReadDbContextAdapter(ApplicationDbContext context)
             => _context = context;

        public IQueryable<T> Query<T>()
            where T : class
        {
            return _context.Set<T>()
                           .AsNoTracking();
        }
    }
}
