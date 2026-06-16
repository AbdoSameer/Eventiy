using Application.Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Abstractions.Persistence
{
    public interface IUnitOfWork
    {
        Task<Result> CommitAsync(CancellationToken cancellationToken = default);
    }
}
