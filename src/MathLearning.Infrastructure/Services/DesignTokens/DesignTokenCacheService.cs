using System.Text.Json;
using MathLearning.Application.DTOs.DesignTokens;
using MathLearning.Application.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace MathLearning.Infrastructure.Services.DesignTokens;

public sealed class DesignTokenCacheService : IDesignTokenCacheService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IMemoryCache memoryCache;
    private readonly IOptions<DesignTokenOptions> options;
    private readonly IConnectionMultiplexer? redis;

    public DesignTokenCacheService(
        IMemoryCache memoryCache,
        IOptions<DesignTokenOptions> options,
        IConnectionMultiplexer? redis = null)
    {
        this.memoryCache = memoryCache;
        this.options = options;
        this.redis = redis;
    }

    public string BuildKey(string version, string theme) => $"tokens:{version}:{theme}";

    public async Task<DesignTokensResponse?> GetAsync(string version, string theme, CancellationToken cancellationToken)
    {
        var key = BuildKey(version, theme);
        if (memoryCache.TryGetValue<DesignTokensResponse>(key, out var cached))
        {
            return cached;
        }

        string? payload = null;
        if (redis is not null)
        {
            var redisValue = await redis.GetDatabase().StringGetAsync(key);
            payload = redisValue.IsNullOrEmpty ? null : redisValue.ToString();
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        cached = JsonSerializer.Deserialize<DesignTokensResponse>(payload, SerializerOptions);
        if (cached is not null)
        {
            memoryCache.Set(key, cached, new MemoryCacheEntryOptions
            {
                Size = 1,
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(options.Value.CacheTtlMinutes)
            });
        }

        return cached;
    }

    public async Task SetAsync(string version, string theme, DesignTokensResponse response, CancellationToken cancellationToken)
    {
        var key = BuildKey(version, theme);
        var payload = JsonSerializer.Serialize(response, SerializerOptions);

        memoryCache.Set(key, response, new MemoryCacheEntryOptions
        {
            Size = 1,
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(options.Value.CacheTtlMinutes)
        });

        if (redis is not null)
        {
            await redis.GetDatabase().StringSetAsync(
                key,
                payload,
                TimeSpan.FromMinutes(options.Value.CacheTtlMinutes));
        }
    }

    public async Task RemoveAsync(string version, string theme, CancellationToken cancellationToken)
    {
        var key = BuildKey(version, theme);
        memoryCache.Remove(key);
        if (redis is not null)
        {
            await redis.GetDatabase().KeyDeleteAsync(key);
        }
    }
}
