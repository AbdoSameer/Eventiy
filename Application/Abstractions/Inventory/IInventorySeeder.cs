using Domain.Aggregates.EventAggregate;

namespace Application.Abstractions.Inventory;

/// <summary>
/// Abstracts the force-seeding and clearing of the distributed inventory
/// counters (Redis) during a strategy handover. Implemented in
/// Infrastructure by <c>RedisInventorySeeder</c>.
/// </summary>
public interface IInventorySeeder
{
    /// <summary>
    /// Force-sets the Redis counter for each ticket type to its current
    /// SQL <c>AvailableCount</c>. Overwrites any stale value.
    /// </summary>
    Task SeedAsync(Event @event, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the Redis counters for all ticket types of the event,
    /// so the next booking falls back to the SQL optimistic path.
    /// </summary>
    Task ClearAsync(Event @event, CancellationToken cancellationToken = default);
}
