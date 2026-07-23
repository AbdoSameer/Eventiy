using Domain.Common;

namespace Application.Abstractions.Inventory;

public interface IInventoryReconciliationService
{
    Task<Result> ReconcileAsync(CancellationToken ct);
}
