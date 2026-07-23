namespace Application.Abstractions.Inventory;

public interface IInventoryCounterStore
{
    Task<long?> GetRemainingAsync(string ticketTypeId, CancellationToken ct);
    Task SetRemainingAsync(string ticketTypeId, long value, CancellationToken ct);
}
