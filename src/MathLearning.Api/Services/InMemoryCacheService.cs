using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace MathLearning.Api.Services;

public class InMemoryCacheService
{
    private readonly IMemoryCache _cache;

    public InMemoryCacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl)
    {
        _cache.Set(key, value, ttl);
        return Task.CompletedTask;
    }

    public Task<T?> GetAsync<T>(string key)
    {
        if (_cache.TryGetValue<T>(key, out var v))
            return Task.FromResult<T?>(v);
        return Task.FromResult<T?>(default);
    }
}