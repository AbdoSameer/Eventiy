using Domain.Aggregates.EventAggregate;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Abstractions.Persistence
{
    public interface IApplicationReadDbContext
    {
        IQueryable<TEntity> Query<TEntity>()
            where TEntity : class;
    }
}
