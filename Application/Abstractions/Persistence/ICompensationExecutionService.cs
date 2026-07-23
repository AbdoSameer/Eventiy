using Application.Abstractions.Payments;
using Domain.Common;

namespace Application.Abstractions.Persistence;

public interface ICompensationExecutionService
{
    Task<Result> ExecuteAsync(CompensationLogDto log, CancellationToken ct);
}
