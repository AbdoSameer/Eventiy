using Application.Abstractions.Inventory;
using StackExchange.Redis;

namespace Infrastructure.Inventory;

public sealed class RedisInventoryCounterStore : IInventoryCounterStore
{
    private readonly ConnectionMultiplexer _redis;
    private const string InventoryKeyPrefix = "inv:ticket:";

    public RedisInventoryCounterStore(ConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<long?> GetRemainingAsync(string ticketTypeId, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var key = InventoryKeyPrefix + ticketTypeId;
        var value = await db.StringGetAsync(key);
        if (!value.HasValue)
            return null;

        if (long.TryParse(value, out var result))
            return result;

        return null;
    }

    public async Task SetRemainingAsync(string key, long value, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        await db.StringSetAsync(key, value);
    }
}
