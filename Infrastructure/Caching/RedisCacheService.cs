using System.Text.Json;
using Application.Abstractions.Caching;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Infrastructure.Caching;

internal sealed class RedisCacheService : ICacheService
{
    private readonly IDatabase _db;
    private readonly IServer _server;
    private readonly ILogger<RedisCacheService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private const string KeyPrefix = "cache:";

    public RedisCacheService(
        ConnectionMultiplexer multiplexer,
        ILogger<RedisCacheService> logger)
    {
        _logger = logger;
        _db = multiplexer.GetDatabase();
        _server = multiplexer.GetServer(multiplexer.GetEndPoints().First());
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        where T : class
    {
        try
        {
            var redisKey = BuildKey(key);
            var value = await _db.StringGetAsync(redisKey);
            return value.HasValue
                ? JsonSerializer.Deserialize<T>(value!, JsonOptions)
                : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis GET failed for key {Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
        where T : class
    {
        try
        {
            var redisKey = BuildKey(key);
            var serialized = JsonSerializer.Serialize(value, JsonOptions);
            await _db.StringSetAsync(redisKey, serialized, ttl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis SET failed for key {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKey = BuildKey(key);
            await _db.KeyDeleteAsync(redisKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis DELETE failed for key {Key}", key);
        }
    }

    public async Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        try
        {
            var scanPattern = $"{KeyPrefix}{pattern}";
            var keys = new List<RedisKey>();

            await foreach (var key in _server.KeysAsync(pattern: scanPattern))
            {
                keys.Add(key);
            }

            if (keys.Count > 0)
            {
                await _db.KeyDeleteAsync(keys.ToArray());
                _logger.LogInformation("Removed {Count} keys matching pattern {Pattern}", keys.Count, pattern);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis pattern delete failed for pattern {Pattern}", pattern);
        }
    }

    public async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _server.FlushDatabaseAsync();
            _logger.LogInformation("Redis cache cleared");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis FLUSHDB failed");
        }
    }

    private static string BuildKey(string key) => $"{KeyPrefix}{key}";
}
