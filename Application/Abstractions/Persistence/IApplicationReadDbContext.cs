namespace Application.Abstractions.Persistence;

public interface IApplicationReadDbContext
{
    IQueryable<TEntity> Query<TEntity>()
        where TEntity : class;

    Task<List<TEntity>> ToListAsync<TEntity>(
        IQueryable<TEntity> query,
        CancellationToken cancellationToken = default);

    Task<TEntity?> FirstOrDefaultAsync<TEntity>(
        IQueryable<TEntity> query,
        CancellationToken cancellationToken = default);

    Task<bool> AnyAsync<TEntity>(
        IQueryable<TEntity> query,
        System.Linq.Expressions.Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync<TEntity>(
        IQueryable<TEntity> query,
        CancellationToken cancellationToken = default);
}