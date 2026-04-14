using System.Text.Json;
using MathLearning.Application.DTOs.Explanations;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace MathLearning.Api.Services;

public sealed class ExplanationCacheService : IExplanationCacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan TimeToLive = TimeSpan.FromHours(12);

    private readonly IMemoryCache _memoryCache;
    private readonly ApiDbContext _db;
    private readonly ILogger<ExplanationCacheService> _logger;
    private readonly IDatabase? _redisDb;

    public ExplanationCacheService(
        IMemoryCache memoryCache,
        ApiDbContext db,
        IServiceProvider serviceProvider,
        ILogger<ExplanationCacheService> logger)
    {
        _memoryCache = memoryCache;
        _db = db;
        _logger = logger;
        _redisDb = serviceProvider.GetService(typeof(IConnectionMultiplexer)) is IConnectionMultiplexer redis
            ? redis.GetDatabase()
            : null;
    }

    public Task<ExplanationResponseDto?> GetExplanationAsync(string problemHash, int grade, string difficulty, string language, CancellationToken ct = default) =>
        GetAsync<ExplanationResponseDto>(BuildScopedHash("explanation", problemHash, language), grade, difficulty, ct, dto => dto with { ServedFromCache = true });

    public Task<MistakeAnalysisResponseDto?> GetMistakeAnalysisAsync(string problemHash, int grade, string difficulty, string language, CancellationToken ct = default) =>
        GetAsync<MistakeAnalysisResponseDto>(BuildScopedHash("mistake", problemHash, language), grade, difficulty, ct, dto => dto with { ServedFromCache = true });

    public Task SetExplanationAsync(string problemHash, int grade, string difficulty, string language, ExplanationResponseDto response, CancellationToken ct = default) =>
        SetAsync(BuildScopedHash("explanation", problemHash, language), grade, difficulty, response with { ServedFromCache = false }, ct);

    public Task SetMistakeAnalysisAsync(string problemHash, int grade, string difficulty, string language, MistakeAnalysisResponseDto response, CancellationToken ct = default) =>
        SetAsync(BuildScopedHash("mistake", problemHash, language), grade, difficulty, response with { ServedFromCache = false }, ct);

    private async Task<T?> GetAsync<T>(
        string scopedHash,
        int grade,
        string difficulty,
        CancellationToken ct,
        Func<T, T> markServedFromCache)
        where T : class
    {
        var memoryKey = BuildMemoryKey(scopedHash, grade, difficulty);
        if (_memoryCache.TryGetValue<T>(memoryKey, out var cached) && cached is not null)
            return markServedFromCache(cached);

        if (_redisDb is not null)
        {
            try
            {
                var redisValue = await _redisDb.StringGetAsync(memoryKey);
                if (redisValue.HasValue)
                {
                    var redisDto = JsonSerializer.Deserialize<T>(redisValue!, JsonOptions);
                    if (redisDto is not null)
                    {
                        Remember(memoryKey, redisDto);
                        return markServedFromCache(redisDto);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis explanation cache lookup failed for {CacheKey}.", memoryKey);
            }
        }

        var dbEntry = await _db.StepExplanationCacheEntries
            .FirstOrDefaultAsync(x =>
                x.ProblemHash == scopedHash &&
                x.Grade == grade &&
                x.Difficulty == difficulty,
                ct);

        if (dbEntry is null || dbEntry.ExpiresAt <= DateTime.UtcNow)
            return null;

        var dto = JsonSerializer.Deserialize<T>(dbEntry.PayloadJson, JsonOptions);
        if (dto is null)
            return null;

        dbEntry.RefreshExpiry(DateTime.UtcNow.Add(TimeToLive));
        await _db.SaveChangesAsync(ct);

        Remember(memoryKey, dto);
        return markServedFromCache(dto);
    }

    private async Task SetAsync<T>(string scopedHash, int grade, string difficulty, T response, CancellationToken ct)
        where T : class
    {
        var memoryKey = BuildMemoryKey(scopedHash, grade, difficulty);
        Remember(memoryKey, response);

        var payload = JsonSerializer.Serialize(response, JsonOptions);

        if (_redisDb is not null)
        {
            try
            {
                await _redisDb.StringSetAsync(memoryKey, payload, TimeToLive);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis explanation cache write failed for {CacheKey}.", memoryKey);
            }
        }

        var expiresAt = DateTime.UtcNow.Add(TimeToLive);
        var existing = await _db.StepExplanationCacheEntries
            .FirstOrDefaultAsync(x =>
                x.ProblemHash == scopedHash &&
                x.Grade == grade &&
                x.Difficulty == difficulty,
                ct);

        if (existing is null)
        {
            _db.StepExplanationCacheEntries.Add(new StepExplanationCacheEntry(scopedHash, grade, difficulty, payload, expiresAt));
        }
        else
        {
            existing.SetPayloadJson(payload);
            existing.RefreshExpiry(expiresAt);
        }

        await _db.SaveChangesAsync(ct);
    }

    private void Remember<T>(string memoryKey, T response)
        where T : class
    {
        _memoryCache.Set(
            memoryKey,
            response,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeToLive,
                Size = 1
            });
    }

    private static string BuildScopedHash(string kind, string problemHash, string language) =>
        $"{kind}:{language.ToLowerInvariant()}:{problemHash}";

    private static string BuildMemoryKey(string scopedHash, int grade, string difficulty) =>
        $"explanation-cache:{scopedHash}:{grade}:{difficulty.ToLowerInvariant()}";
}
