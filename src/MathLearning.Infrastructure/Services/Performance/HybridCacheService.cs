using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace MathLearning.Infrastructure.Services.Performance;

public sealed class HybridCacheService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IMemoryCache memoryCache;
    private readonly IConnectionMultiplexer? redis;
    private readonly ILogger<HybridCacheService> logger;

    public HybridCacheService(
        IMemoryCache memoryCache,
        ILogger<HybridCacheService> logger,
        IConnectionMultiplexer? redis = null)
    {
        this.memoryCache = memoryCache;
        this.logger = logger;
        this.redis = redis;
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan memoryTtl,
        TimeSpan? redisTtl = null,
        CancellationToken cancellationToken = default)
    {
        if (memoryCache.TryGetValue<T>(key, out var cached) && cached is not null)
        {
            return cached;
        }

        if (redis is not null)
        {
            try
            {
                var redisDb = redis.GetDatabase();
                var payload = await redisDb.StringGetAsync(key);
                if (payload.HasValue)
                {
                    var hydrated = JsonSerializer.Deserialize<T>(payload!, SerializerOptions);
                    if (hydrated is not null)
                    {
                        SetMemory(key, hydrated, memoryTtl);
                        return hydrated;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Redis cache read failed for key {CacheKey}.", key);
            }
        }

        var created = await factory(cancellationToken);
        await SetAsync(key, created, memoryTtl, redisTtl, cancellationToken);
        return created;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (memoryCache.TryGetValue<T>(key, out var cached) && cached is not null)
        {
            return cached;
        }

        if (redis is null)
        {
            return default;
        }

        try
        {
            var payload = await redis.GetDatabase().StringGetAsync(key);
            if (!payload.HasValue)
            {
                return default;
            }

            var hydrated = JsonSerializer.Deserialize<T>(payload!, SerializerOptions);
            if (hydrated is not null)
            {
                SetMemory(key, hydrated, TimeSpan.FromSeconds(30));
            }

            return hydrated;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis cache read failed for key {CacheKey}.", key);
            return default;
        }
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan memoryTtl,
        TimeSpan? redisTtl = null,
        CancellationToken cancellationToken = default)
    {
        SetMemory(key, value, memoryTtl);

        if (redis is null)
        {
            return;
        }

        try
        {
            var redisDb = redis.GetDatabase();
            var payload = JsonSerializer.Serialize(value, SerializerOptions);
            await redisDb.StringSetAsync(key, payload, redisTtl ?? memoryTtl);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis cache write failed for key {CacheKey}.", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        memoryCache.Remove(key);

        if (redis is null)
        {
            return;
        }

        try
        {
            await redis.GetDatabase().KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis cache delete failed for key {CacheKey}.", key);
        }
    }

    private void SetMemory<T>(string key, T value, TimeSpan ttl)
    {
        memoryCache.Set(
            key,
            value,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl,
                Priority = CacheItemPriority.Normal,
                Size = 1
            });
    }
}
