using System.Collections.Concurrent;
using Application.Abstractions.Caching;

namespace Eventy.Testing.Foundation.Fakes;

/// <summary>
/// In-memory cache fake for integration tests.
/// No Redis dependency — behaves like cache but stores in a ConcurrentDictionary.
/// </summary>
public sealed class FakeCacheService : ICacheService
{
    private readonly ConcurrentDictionary<string, object> _cache = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        _cache.TryGetValue(key, out var value);
        return Task.FromResult(value as T);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) where T : class
    {
        _cache[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _cache.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        // Simple prefix match for test fake (Redis uses glob-style patterns)
        var keysToRemove = _cache.Keys
            .Where(k => k.StartsWith(pattern.Replace("*", "")))
            .ToList();

        foreach (var key in keysToRemove)
            _cache.TryRemove(key, out _);

        return Task.CompletedTask;
    }

    public Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        _cache.Clear();
        return Task.CompletedTask;
    }
}
