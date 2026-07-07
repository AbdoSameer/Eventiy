using Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Infrastructure.Persistence;

internal sealed class ReadDbContextAdapter : IApplicationReadDbContext
{
    private readonly ApplicationDbContext _context;

    public ReadDbContextAdapter(ApplicationDbContext context)
    {
        _context = context;
    }

    public IQueryable<T> Query<T>() where T : class
        => _context.Set<T>().AsNoTracking();

    public Task<List<TEntity>> ToListAsync<TEntity>(
        IQueryable<TEntity> query,
        CancellationToken cancellationToken = default)
        => query.ToListAsync(cancellationToken);

    public Task<TEntity?> FirstOrDefaultAsync<TEntity>(
        IQueryable<TEntity> query,
        CancellationToken cancellationToken = default)
        => query.FirstOrDefaultAsync(cancellationToken);

    public Task<bool> AnyAsync<TEntity>(
        IQueryable<TEntity> query,
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
        => query.AnyAsync(predicate, cancellationToken);

    public Task<int> CountAsync<TEntity>(
        IQueryable<TEntity> query,
        CancellationToken cancellationToken = default)
        => query.CountAsync(cancellationToken);
}