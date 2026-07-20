using Application.Abstractions.Inventory;
using Domain.Aggregates.EventAggregate;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Infrastructure.Inventory;

/// <summary>
/// Force-seeds or clears the Redis inventory counters for an event's
/// ticket types. Used by the ToggleHighDemandCommandHandler during the
/// atomic handover (Layer 1). Unlike the lazy SETNX seeding in
/// <see cref="AtomicRedisReservationStrategy"/>, this implementation
/// unconditionally overwrites the counter with the SQL value.
/// </summary>
public sealed class RedisInventorySeeder : IInventorySeeder
{
    private const string InventoryKeyPrefix = "inv:ticket:";

    private readonly ConnectionMultiplexer _redis;
    private readonly ILogger<RedisInventorySeeder> _logger;

    public RedisInventorySeeder(
        ConnectionMultiplexer redis,
        ILogger<RedisInventorySeeder> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task SeedAsync(Event @event, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();

        foreach (var ticketType in @event.TicketTypes)
        {
            var key = InventoryKeyPrefix + ticketType.Id.Value;
            await db.StringSetAsync(key, ticketType.AvailableCount);
        }

        _logger.LogInformation(
            "Force-seeded Redis inventory for event {EventId} with {Count} ticket types",
            @event.Id.Value, @event.TicketTypes.Count);
    }

    public async Task ClearAsync(Event @event, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();

        foreach (var ticketType in @event.TicketTypes)
        {
            var key = InventoryKeyPrefix + ticketType.Id.Value;
            await db.KeyDeleteAsync(key);
        }

        _logger.LogInformation(
            "Cleared Redis inventory for event {EventId} with {Count} ticket types",
            @event.Id.Value, @event.TicketTypes.Count);
    }
}
