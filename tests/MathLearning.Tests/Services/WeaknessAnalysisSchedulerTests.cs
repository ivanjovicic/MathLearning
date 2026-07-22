using MathLearning.Api.Services;
using MathLearning.Application.DTOs.Analytics;
using MathLearning.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MathLearning.Tests.Services;

public sealed class WeaknessAnalysisSchedulerTests
{
    [Fact]
    public async Task DuplicateEnqueues_CoalesceToOnePendingJob()
    {
        await using var harness = CreateHarness(queueCapacity: 8, timeoutSeconds: 30);

        for (var i = 0; i < 10_000; i++)
            Assert.True(harness.Scheduler.Enqueue(harness.UserId));

        await harness.Scheduler.StartAsync(CancellationToken.None);
        await harness.Service.Started.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, harness.Service.CallCount);

        harness.Service.Release();
        await harness.Scheduler.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task BoundedQueue_RejectsDistinctWorkWhenCapacityIsExceeded()
    {
        await using var harness = CreateHarness(queueCapacity: 1, timeoutSeconds: 30);

        await harness.Scheduler.StartAsync(CancellationToken.None);

        Assert.True(harness.Scheduler.Enqueue(Guid.NewGuid()));
        await harness.Service.Started.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(harness.Scheduler.Enqueue(Guid.NewGuid()));
        Assert.False(harness.Scheduler.Enqueue(Guid.NewGuid()));

        harness.Service.Release();
        await harness.Scheduler.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_CancelsRunningJob()
    {
        await using var harness = CreateHarness(queueCapacity: 1, timeoutSeconds: 30);

        await harness.Scheduler.StartAsync(CancellationToken.None);

        Assert.True(harness.Scheduler.Enqueue(Guid.NewGuid()));
        await harness.Service.Started.WaitAsync(TimeSpan.FromSeconds(5));

        await harness.Scheduler.StopAsync(CancellationToken.None);

        Assert.True(harness.Service.WasCancelled);
        Assert.Equal(1, harness.Service.CallCount);
    }

    private static SchedulerHarness CreateHarness(int queueCapacity, int timeoutSeconds)
    {
        var service = new BlockingWeaknessAnalysisService();
        var services = new ServiceCollection();
        services.AddSingleton<IWeaknessAnalysisService>(service);
        services.AddSingleton<IOptions<WeaknessAnalysisSchedulerOptions>>(
            Options.Create(new WeaknessAnalysisSchedulerOptions
            {
                QueueCapacity = queueCapacity,
                UserAnalysisTimeoutSeconds = timeoutSeconds
            }));
        services.AddSingleton<ILogger<WeaknessAnalysisScheduler>>(NullLogger<WeaknessAnalysisScheduler>.Instance);

        var provider = services.BuildServiceProvider();
        var scheduler = new WeaknessAnalysisScheduler(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IOptions<WeaknessAnalysisSchedulerOptions>>(),
            provider.GetRequiredService<ILogger<WeaknessAnalysisScheduler>>());

        return new SchedulerHarness(provider, scheduler, service, Guid.NewGuid());
    }

    private sealed record SchedulerHarness(
        ServiceProvider Provider,
        WeaknessAnalysisScheduler Scheduler,
        BlockingWeaknessAnalysisService Service,
        Guid UserId) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Provider.DisposeAsync();
    }

    private sealed class BlockingWeaknessAnalysisService : IWeaknessAnalysisService
    {
        private readonly TaskCompletionSource<Guid> started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int callCount;

        public Task<Guid> Started => started.Task;

        public int CallCount => Volatile.Read(ref callCount);

        public bool WasCancelled { get; private set; }

        public void Release() => release.TrySetResult();

        public async Task AnalyzeUserAsync(Guid userId, CancellationToken ct)
        {
            Interlocked.Increment(ref callCount);
            started.TrySetResult(userId);

            try
            {
                await release.Task.WaitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                WasCancelled = true;
                throw;
            }
        }

        public Task<IReadOnlyList<WeakTopicDto>> GetWeakTopicsAsync(Guid userId, int take = 5, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<WeakTopicDto>>(Array.Empty<WeakTopicDto>());

        public Task<IReadOnlyList<WeakSubtopicDto>> GetWeakSubtopicsAsync(Guid userId, int take = 10, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<WeakSubtopicDto>>(Array.Empty<WeakSubtopicDto>());

        public Task<IReadOnlyList<PracticeRecommendationDto>> GeneratePracticeRecommendationsAsync(Guid userId, int take = 10, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PracticeRecommendationDto>>(Array.Empty<PracticeRecommendationDto>());
    }
}
