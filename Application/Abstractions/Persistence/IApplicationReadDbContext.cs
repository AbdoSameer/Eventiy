
namespace Application.Abstractions.Persistence
{
    public interface IApplicationReadDbContext
    {
        IQueryable<TEntity> Query<TEntity>()
            where TEntity : class;
    }
}
    