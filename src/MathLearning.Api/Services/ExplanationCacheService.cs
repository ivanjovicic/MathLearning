using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
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
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> LocalSingleFlights = new(StringComparer.Ordinal);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan TimeToLive = TimeSpan.FromHours(12);
    private static readonly TimeSpan DistributedLeaseTimeToLive = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DistributedLeaseWaitBudget = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DistributedLeasePollInterval = TimeSpan.FromMilliseconds(150);
    private const int MaxPayloadBytes = 256 * 1024;

    private readonly IMemoryCache memoryCache;
    private readonly ApiDbContext db;
    private readonly ExplanationCacheMetrics metrics;
    private readonly ILogger<ExplanationCacheService> logger;
    private readonly IDatabase? redisDb;

    public ExplanationCacheService(
        IMemoryCache memoryCache,
        ApiDbContext db,
        IServiceProvider serviceProvider,
        ExplanationCacheMetrics metrics,
        ILogger<ExplanationCacheService> logger)
    {
        this.memoryCache = memoryCache;
        this.db = db;
        this.metrics = metrics;
        this.logger = logger;
        redisDb = serviceProvider.GetService(typeof(IConnectionMultiplexer)) is IConnectionMultiplexer redis
            ? redis.GetDatabase()
            : null;
    }

    public Task<ExplanationResponseDto?> GetExplanationAsync(
        string problemHash,
        int grade,
        string difficulty,
        string language,
        CancellationToken ct = default) =>
        TryReadAsync<ExplanationResponseDto>(
            BuildScopedHash("explanation", problemHash, language),
            grade,
            difficulty,
            ct,
            response => response with { ServedFromCache = true });

    public Task<MistakeAnalysisResponseDto?> GetMistakeAnalysisAsync(
        string problemHash,
        int grade,
        string difficulty,
        string language,
        CancellationToken ct = default) =>
        TryReadAsync<MistakeAnalysisResponseDto>(
            BuildScopedHash("mistake", problemHash, language),
            grade,
            difficulty,
            ct,
            response => response with { ServedFromCache = true });

    public Task<ExplanationResponseDto> GetOrCreateExplanationAsync(
        string problemHash,
        int grade,
        string difficulty,
        string language,
        bool forceRefresh,
        Func<CancellationToken, Task<ExplanationResponseDto>> factory,
        CancellationToken ct = default) =>
        GetOrCreateAsync(
            "explanation",
            problemHash,
            grade,
            difficulty,
            language,
            forceRefresh,
            factory,
            response => response with { ServedFromCache = true },
            ct);

    public Task<MistakeAnalysisResponseDto> GetOrCreateMistakeAnalysisAsync(
        string problemHash,
        int grade,
        string difficulty,
        string language,
        bool forceRefresh,
        Func<CancellationToken, Task<MistakeAnalysisResponseDto>> factory,
        CancellationToken ct = default) =>
        GetOrCreateAsync(
            "mistake",
            problemHash,
            grade,
            difficulty,
            language,
            forceRefresh,
            factory,
            response => response with { ServedFromCache = true },
            ct);

    public Task SetExplanationAsync(
        string problemHash,
        int grade,
        string difficulty,
        string language,
        ExplanationResponseDto response,
        CancellationToken ct = default) =>
        StoreAsync(
            BuildScopedHash("explanation", problemHash, language),
            grade,
            difficulty,
            response with { ServedFromCache = false },
            ct);

    public Task SetMistakeAnalysisAsync(
        string problemHash,
        int grade,
        string difficulty,
        string language,
        MistakeAnalysisResponseDto response,
        CancellationToken ct = default) =>
        StoreAsync(
            BuildScopedHash("mistake", problemHash, language),
            grade,
            difficulty,
            response with { ServedFromCache = false },
            ct);

    public async Task<int> CleanupExpiredEntriesAsync(int batchSize, CancellationToken ct = default)
    {
        if (batchSize <= 0)
        {
            metrics.RecordCleanup(0);
            return 0;
        }

        var now = DateTime.UtcNow;
        var expiredQuery = db.StepExplanationCacheEntries
            .AsNoTracking()
            .Where(x => x.ExpiresAt <= now)
            .OrderBy(x => x.ExpiresAt)
            .ThenBy(x => x.Id)
            .Take(batchSize);

        if (db.Database.IsRelational())
        {
            var expiredIds = await expiredQuery.Select(x => x.Id).ToListAsync(ct);
            if (expiredIds.Count == 0)
            {
                metrics.RecordCleanup(0);
                return 0;
            }

            var deletedCount = await db.StepExplanationCacheEntries
                .Where(x => expiredIds.Contains(x.Id))
                .ExecuteDeleteAsync(ct);

            metrics.RecordCleanup(deletedCount);
            return deletedCount;
        }

        var expiredEntries = await db.StepExplanationCacheEntries
            .Where(x => x.ExpiresAt <= now)
            .OrderBy(x => x.ExpiresAt)
            .ThenBy(x => x.Id)
            .Take(batchSize)
            .ToListAsync(ct);
        if (expiredEntries.Count == 0)
        {
            metrics.RecordCleanup(0);
            return 0;
        }

        db.StepExplanationCacheEntries.RemoveRange(expiredEntries);
        var deleted = await db.SaveChangesAsync(ct);

        metrics.RecordCleanup(deleted);
        return deleted;
    }

    private async Task<T?> TryReadAsync<T>(
        string scopedHash,
        int grade,
        string difficulty,
        CancellationToken ct,
        Func<T, T> markServedFromCache)
        where T : class
    {
        var memoryKey = BuildMemoryKey(scopedHash, grade, difficulty);
        var cached = await TryReadCachedValueAsync<T>(scopedHash, grade, difficulty, memoryKey, ct);
        if (cached is null)
        {
            metrics.RecordMiss();
            return null;
        }

        metrics.RecordHit();
        return markServedFromCache(cached);
    }

    private async Task<T> GetOrCreateAsync<T>(
        string cacheKind,
        string problemHash,
        int grade,
        string difficulty,
        string language,
        bool forceRefresh,
        Func<CancellationToken, Task<T>> factory,
        Func<T, T> markServedFromCache,
        CancellationToken ct)
        where T : class
    {
        var scopedHash = BuildScopedHash(cacheKind, problemHash, language);
        var memoryKey = BuildMemoryKey(scopedHash, grade, difficulty);
        if (!forceRefresh)
        {
            var cached = await TryReadCachedValueAsync<T>(scopedHash, grade, difficulty, memoryKey, ct);
            if (cached is not null)
            {
                metrics.RecordHit();
                return markServedFromCache(cached);
            }

            metrics.RecordMiss();
        }

        var gate = LocalSingleFlights.GetOrAdd(memoryKey, _ => new SemaphoreSlim(1, 1));
        var waitedForLocalGate = !await gate.WaitAsync(0, ct);
        if (waitedForLocalGate)
            await gate.WaitAsync(ct);

        string? leaseToken = null;
        try
        {
            if (waitedForLocalGate)
                metrics.RecordStampedeSuppressed();

            var cachedAfterGate = await TryReadCachedValueAsync<T>(scopedHash, grade, difficulty, memoryKey, ct);
            if (cachedAfterGate is not null)
            {
                metrics.RecordHit();
                return markServedFromCache(cachedAfterGate);
            }

            leaseToken = await TryAcquireDistributedLeaseAsync(memoryKey);
            if (leaseToken is null)
            {
                metrics.RecordStampedeSuppressed();
                var observed = await WaitForCachedValueAsync<T>(scopedHash, grade, difficulty, memoryKey, ct);
                if (observed is not null)
                {
                    metrics.RecordHit();
                    return markServedFromCache(observed);
                }

                logger.LogWarning(
                    "Distributed explanation cache lease could not be acquired for {CacheKey}; generating without a lease.",
                    memoryKey);
            }

            var generationWatch = Stopwatch.StartNew();
            var response = await factory(ct);
            generationWatch.Stop();
            metrics.RecordGeneration(generationWatch.Elapsed);

            await StoreAsync(scopedHash, grade, difficulty, response, ct);
            return response;
        }
        finally
        {
            if (leaseToken is not null)
                await ReleaseDistributedLeaseAsync(memoryKey, leaseToken);

            gate.Release();
        }
    }

    private async Task<T?> WaitForCachedValueAsync<T>(
        string scopedHash,
        int grade,
        string difficulty,
        string memoryKey,
        CancellationToken ct)
        where T : class
    {
        var deadline = DateTime.UtcNow + DistributedLeaseWaitBudget;
        while (DateTime.UtcNow <= deadline)
        {
            var cached = await TryReadCachedValueAsync<T>(scopedHash, grade, difficulty, memoryKey, ct);
            if (cached is not null)
                return cached;

            await Task.Delay(DistributedLeasePollInterval, ct);
        }

        return null;
    }

    private async Task<string?> TryAcquireDistributedLeaseAsync(string memoryKey)
    {
        if (redisDb is null)
            return null;

        var leaseKey = BuildLeaseKey(memoryKey);
        var leaseToken = Guid.NewGuid().ToString("N");
        var acquired = await redisDb.LockTakeAsync(leaseKey, leaseToken, DistributedLeaseTimeToLive);
        return acquired ? leaseToken : null;
    }

    private async Task ReleaseDistributedLeaseAsync(string memoryKey, string leaseToken)
    {
        if (redisDb is null)
            return;

        try
        {
            await redisDb.LockReleaseAsync(BuildLeaseKey(memoryKey), leaseToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Distributed explanation cache lease release failed for {CacheKey}.", memoryKey);
        }
    }

    private async Task<T?> TryReadCachedValueAsync<T>(
        string scopedHash,
        int grade,
        string difficulty,
        string memoryKey,
        CancellationToken ct)
        where T : class
    {
        if (memoryCache.TryGetValue<T>(memoryKey, out var memoryValue) && memoryValue is not null)
            return memoryValue;

        if (redisDb is not null)
        {
            try
            {
                var redisValue = await redisDb.StringGetAsync(memoryKey);
                if (redisValue.HasValue)
                {
                    var redisDto = Deserialize<T>(redisValue!);
                    if (redisDto is not null)
                    {
                        Remember(memoryKey, redisDto);
                        return redisDto;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Redis explanation cache lookup failed for {CacheKey}.", memoryKey);
            }
        }

        var now = DateTime.UtcNow;
        var dbEntry = await db.StepExplanationCacheEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.ProblemHash == scopedHash &&
                x.Grade == grade &&
                x.Difficulty == difficulty &&
                x.ExpiresAt > now,
                ct);

        if (dbEntry is null)
            return null;

        var dbDto = Deserialize<T>(dbEntry.PayloadJson);
        if (dbDto is null)
            return null;

        Remember(memoryKey, dbDto);
        return dbDto;
    }

    private async Task StoreAsync<T>(
        string scopedHash,
        int grade,
        string difficulty,
        T response,
        CancellationToken ct)
        where T : class
    {
        var memoryKey = BuildMemoryKey(scopedHash, grade, difficulty);
        var payload = JsonSerializer.Serialize(response, JsonOptions);
        if (Encoding.UTF8.GetByteCount(payload) > MaxPayloadBytes)
        {
            metrics.RecordOversizedPayloadSkip();
            logger.LogWarning(
                "Explanation cache payload for {CacheKey} exceeded the {MaxPayloadBytes} byte boundary and was not cached.",
                memoryKey,
                MaxPayloadBytes);
            return;
        }

        var now = DateTime.UtcNow;
        var expiresAt = now.Add(TimeToLive);

        if (db.Database.IsRelational())
        {
            try
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO ""step_explanation_cache"" (""Id"", ""ProblemHash"", ""Grade"", ""Difficulty"", ""PayloadJson"", ""CreatedAt"", ""ExpiresAt"", ""LastAccessedAt"")
VALUES ({Guid.NewGuid()}, {scopedHash}, {grade}, {difficulty}, {payload}, {now}, {expiresAt}, {now})
ON CONFLICT (""ProblemHash"", ""Grade"", ""Difficulty"")
DO UPDATE SET
    ""PayloadJson"" = EXCLUDED.""PayloadJson"",
    ""ExpiresAt"" = EXCLUDED.""ExpiresAt"",
    ""LastAccessedAt"" = EXCLUDED.""LastAccessedAt"";
");
            }
            catch (Exception ex)
            {
                metrics.RecordWriteFailure();
                logger.LogWarning(ex, "Database explanation cache write failed for {CacheKey}.", memoryKey);
                return;
            }
        }
        else
        {
            try
            {
                var existing = await db.StepExplanationCacheEntries
                    .FirstOrDefaultAsync(x =>
                        x.ProblemHash == scopedHash &&
                        x.Grade == grade &&
                        x.Difficulty == difficulty,
                        ct);

                if (existing is null)
                {
                    db.StepExplanationCacheEntries.Add(new StepExplanationCacheEntry(scopedHash, grade, difficulty, payload, expiresAt));
                }
                else
                {
                    existing.SetPayloadJson(payload);
                    existing.RefreshExpiry(expiresAt);
                }

                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                metrics.RecordWriteFailure();
                logger.LogWarning(ex, "Database explanation cache write failed for {CacheKey}.", memoryKey);
                return;
            }
        }

        if (redisDb is not null)
        {
            try
            {
                await redisDb.StringSetAsync(memoryKey, payload, TimeToLive);
            }
            catch (Exception ex)
            {
                metrics.RecordWriteFailure();
                logger.LogWarning(ex, "Redis explanation cache write failed for {CacheKey}.", memoryKey);
            }
        }

        Remember(memoryKey, response);
    }

    private void Remember<T>(string memoryKey, T response)
        where T : class
    {
        memoryCache.Set(
            memoryKey,
            response,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeToLive,
                Size = 1
            });
    }

    private static T? Deserialize<T>(string payload)
        where T : class =>
        JsonSerializer.Deserialize<T>(payload, JsonOptions);

    private static string BuildScopedHash(string kind, string problemHash, string language) =>
        $"{kind}:{NormalizeKeyPart(language)}:{NormalizeKeyPart(problemHash)}";

    private static string BuildMemoryKey(string scopedHash, int grade, string difficulty) =>
        $"explanation-cache:{scopedHash}:{grade}:{NormalizeKeyPart(difficulty)}";

    private static string BuildLeaseKey(string memoryKey) =>
        $"explanation-cache:lease:{memoryKey}";

    private static string NormalizeKeyPart(string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
}
