using MathLearning.Api.Services;
using MathLearning.Application.DTOs.Explanations;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace MathLearning.Tests.Services;

public sealed class ExplanationCacheServiceTests
{
    [Fact]
    public async Task GetExplanationAsync_DoesNotMutateStoredRowOnRead()
    {
        await using var db = TestDbContextFactory.Create();
        var cache = CreateCacheService(db);

        await cache.SetExplanationAsync(
            problemHash: "hash-1",
            grade: 5,
            difficulty: "easy",
            language: "en",
            response: CreateExplanationResponse());

        var scopedHash = "explanation:en:hash-1";
        var before = await db.StepExplanationCacheEntries.AsNoTracking().SingleAsync(x =>
            x.ProblemHash == scopedHash &&
            x.Grade == 5 &&
            x.Difficulty == "easy");

        var result = await cache.GetExplanationAsync("hash-1", 5, "easy", "en");

        var after = await db.StepExplanationCacheEntries.AsNoTracking().SingleAsync(x =>
            x.ProblemHash == scopedHash &&
            x.Grade == 5 &&
            x.Difficulty == "easy");

        Assert.NotNull(result);
        Assert.True(result!.ServedFromCache);
        Assert.Equal(before.LastAccessedAt, after.LastAccessedAt);
    }

    [Fact]
    public async Task GetOrCreateExplanationAsync_UsesSingleFlightForConcurrentRequests()
    {
        await using var db = TestDbContextFactory.Create();
        var cache = CreateCacheService(db);
        var calls = 0;

        Task<ExplanationResponseDto> Factory(CancellationToken ct) =>
            Task.Run(async () =>
            {
                Interlocked.Increment(ref calls);
                await Task.Delay(150, ct);
                return CreateExplanationResponse();
            }, ct);

        var first = cache.GetOrCreateExplanationAsync("hash-2", 6, "medium", "en", false, Factory);
        var second = cache.GetOrCreateExplanationAsync("hash-2", 6, "medium", "en", false, Factory);

        var results = await Task.WhenAll(first, second);

        Assert.Equal(1, calls);
        Assert.Contains(results, response => response.ServedFromCache);
        Assert.Contains(results, response => !response.ServedFromCache);
    }

    [Fact]
    public async Task GetOrCreateExplanationAsync_ForceRefreshIsCoalescedAndRateLimited()
    {
        await using var db = TestDbContextFactory.Create();
        var cache = CreateCacheService(db);

        await cache.SetExplanationAsync(
            problemHash: "hash-force",
            grade: 6,
            difficulty: "hard",
            language: "en",
            response: CreateExplanationResponse(problemHash: "cached-hash"));

        var calls = 0;

        Task<ExplanationResponseDto> Factory(CancellationToken ct) =>
            Task.Run(async () =>
            {
                Interlocked.Increment(ref calls);
                await Task.Delay(150, ct);
                return CreateExplanationResponse(problemHash: "forced-hash");
            }, ct);

        var first = cache.GetOrCreateExplanationAsync("hash-force", 6, "hard", "en", true, Factory);
        var second = cache.GetOrCreateExplanationAsync("hash-force", 6, "hard", "en", true, Factory);

        var results = await Task.WhenAll(first, second);

        Assert.Equal(1, calls);
        Assert.All(results, result => Assert.Equal("forced-hash", result.ProblemHash));
        Assert.Contains(results, response => response.ServedFromCache);
        Assert.Contains(results, response => !response.ServedFromCache);

        var third = await cache.GetOrCreateExplanationAsync("hash-force", 6, "hard", "en", true, Factory);

        Assert.Equal(1, calls);
        Assert.Equal("forced-hash", third.ProblemHash);
        Assert.True(third.ServedFromCache);
    }

    [Fact]
    public async Task GetOrCreateExplanationAsync_WaitingCancellationDoesNotCancelSharedGeneration()
    {
        await using var db = TestDbContextFactory.Create();
        var cache = CreateCacheService(db);

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;

        Task<ExplanationResponseDto> Factory(CancellationToken ct) =>
            Task.Run(async () =>
            {
                Interlocked.Increment(ref calls);
                started.TrySetResult();
                await release.Task.WaitAsync(ct);
                return CreateExplanationResponse(problemHash: "cancel-shared");
            }, ct);

        using var ownerCts = new CancellationTokenSource();
        using var waiterCts = new CancellationTokenSource();

        var ownerTask = cache.GetOrCreateExplanationAsync("hash-cancel", 7, "easy", "en", false, Factory, ownerCts.Token);
        await started.Task;

        var waiterTask = cache.GetOrCreateExplanationAsync("hash-cancel", 7, "easy", "en", false, Factory, waiterCts.Token);
        waiterCts.Cancel();

        release.SetResult();

        var owner = await ownerTask;
        await Assert.ThrowsAsync<OperationCanceledException>(() => waiterTask);

        Assert.Equal(1, calls);
        Assert.Equal("cancel-shared", owner.ProblemHash);
        Assert.False(owner.ServedFromCache);
    }

    [Fact]
    public async Task CleanupExpiredEntriesAsync_DeletesAtMostRequestedBatch()
    {
        await using var db = TestDbContextFactory.Create();
        var cache = CreateCacheService(db);

        db.StepExplanationCacheEntries.AddRange(
            new MathLearning.Domain.Entities.StepExplanationCacheEntry("hash-a", 1, "easy", "{}", DateTime.UtcNow.AddMinutes(-10)),
            new MathLearning.Domain.Entities.StepExplanationCacheEntry("hash-b", 2, "easy", "{}", DateTime.UtcNow.AddMinutes(-5)),
            new MathLearning.Domain.Entities.StepExplanationCacheEntry("hash-c", 3, "easy", "{}", DateTime.UtcNow.AddHours(1)));
        await db.SaveChangesAsync();

        var deleted = await cache.CleanupExpiredEntriesAsync(1);

        Assert.Equal(1, deleted);
        Assert.Equal(2, await db.StepExplanationCacheEntries.CountAsync());
    }

    [Fact]
    public async Task GetExplanationAsync_TreatsExpiredRowAsMissBeforeCleanup()
    {
        await using var db = TestDbContextFactory.Create();
        var cache = CreateCacheService(db);

        await cache.SetExplanationAsync(
            problemHash: "hash-expired",
            grade: 4,
            difficulty: "easy",
            language: "en",
            response: CreateExplanationResponse(problemHash: "expired-hash"));

        var entry = await db.StepExplanationCacheEntries.SingleAsync();
        entry.RefreshExpiry(DateTime.UtcNow.AddMinutes(-1));
        await db.SaveChangesAsync();

        var expiredCache = CreateCacheService(db);
        var response = await expiredCache.GetExplanationAsync("hash-expired", 4, "easy", "en");
        var deleted = await expiredCache.CleanupExpiredEntriesAsync(10);

        Assert.Null(response);
        Assert.Equal(1, deleted);
        Assert.Empty(await db.StepExplanationCacheEntries.ToListAsync());
    }

    private static ExplanationCacheService CreateCacheService(MathLearning.Infrastructure.Persistance.ApiDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMemoryCache>(_ => new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 }));
        services.AddLogging();

        var provider = services.BuildServiceProvider();

        return new ExplanationCacheService(
            provider.GetRequiredService<IMemoryCache>(),
            db,
            provider,
            new ExplanationCacheMetrics(),
            NullLogger<ExplanationCacheService>.Instance);
    }

    private static ExplanationResponseDto CreateExplanationResponse(string problemHash = "hash") => new(
        ProblemId: 12,
        ProblemText: "2 + 2",
        ProblemHash: problemHash,
        Language: "en",
        ServedFromCache: false,
        Steps: Array.Empty<StepExplanationItemDto>(),
        FormulaReferences: Array.Empty<FormulaReferenceDto>(),
        Mistakes: Array.Empty<MistakeInsightDto>());
}
