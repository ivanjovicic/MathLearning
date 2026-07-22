using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Channels;
using MathLearning.Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MathLearning.Api.Services;

public sealed class WeaknessAnalysisSchedulerOptions
{
    public const string SectionName = "WeaknessAnalysisScheduler";

    public int QueueCapacity { get; set; } = 256;

    public int UserAnalysisTimeoutSeconds { get; set; } = 300;
}

public interface IWeaknessAnalysisScheduler
{
    bool Enqueue(Guid userId);
}

public sealed class WeaknessAnalysisScheduler : BackgroundService, IWeaknessAnalysisScheduler
{
    private static readonly Meter Meter = new("MathLearning.Api.WeaknessAnalysisScheduler", "1.0.0");
    private static readonly Counter<long> EnqueuedCounter = Meter.CreateCounter<long>(
        "mathlearning.weakness_scheduler.enqueued_total",
        description: "Accepted weakness-analysis jobs.");
    private static readonly Counter<long> DeduplicatedCounter = Meter.CreateCounter<long>(
        "mathlearning.weakness_scheduler.deduplicated_total",
        description: "Duplicate weakness-analysis jobs suppressed while pending or running.");
    private static readonly Counter<long> RejectedCounter = Meter.CreateCounter<long>(
        "mathlearning.weakness_scheduler.rejected_total",
        description: "Weakness-analysis jobs rejected because the bounded queue was full.");
    private static readonly Counter<long> FailedCounter = Meter.CreateCounter<long>(
        "mathlearning.weakness_scheduler.failed_total",
        description: "Weakness-analysis jobs that failed or timed out.");
    private static readonly Histogram<double> ProcessingDurationHistogram = Meter.CreateHistogram<double>(
        "mathlearning.weakness_scheduler.processing_duration_ms",
        unit: "ms",
        description: "Weakness-analysis job processing duration.");

    private readonly ConcurrentDictionary<Guid, WeaknessAnalysisJobState> jobs = new();
    private readonly Channel<Guid> queue;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<WeaknessAnalysisScheduler> logger;
    private readonly TimeSpan analysisTimeout;
    private readonly int queueCapacity;
    private readonly ObservableGauge<int> queueDepthGauge;
    private readonly ObservableGauge<double> oldestJobAgeGauge;

    public WeaknessAnalysisScheduler(
        IServiceScopeFactory scopeFactory,
        IOptions<WeaknessAnalysisSchedulerOptions> options,
        ILogger<WeaknessAnalysisScheduler> logger)
    {
        this.scopeFactory = scopeFactory;
        this.logger = logger;

        var schedulerOptions = options.Value ?? new WeaknessAnalysisSchedulerOptions();
        queueCapacity = Math.Max(1, schedulerOptions.QueueCapacity);
        analysisTimeout = TimeSpan.FromSeconds(Math.Max(1, schedulerOptions.UserAnalysisTimeoutSeconds));

        queue = Channel.CreateBounded<Guid>(new BoundedChannelOptions(queueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false
        });

        queueDepthGauge = Meter.CreateObservableGauge(
            "mathlearning.weakness_scheduler.queue_depth",
            () => new Measurement<int>(GetPendingJobCount()),
            description: "Number of queued weakness-analysis jobs waiting to run.");

        oldestJobAgeGauge = Meter.CreateObservableGauge(
            "mathlearning.weakness_scheduler.oldest_job_age_seconds",
            () => new Measurement<double>(GetOldestPendingJobAgeSeconds()),
            unit: "s",
            description: "Age in seconds of the oldest pending weakness-analysis job.");
    }

    public bool Enqueue(Guid userId)
    {
        if (jobs.ContainsKey(userId))
        {
            DeduplicatedCounter.Add(1);
            logger.LogDebug(
                "Weakness analysis job coalesced for user. UserId={UserId} PendingJobs={PendingJobs}",
                userId,
                GetPendingJobCount());
            return true;
        }

        var state = new WeaknessAnalysisJobState(DateTimeOffset.UtcNow);
        if (!jobs.TryAdd(userId, state))
        {
            DeduplicatedCounter.Add(1);
            return true;
        }

        if (!queue.Writer.TryWrite(userId))
        {
            jobs.TryRemove(userId, out _);
            RejectedCounter.Add(1);
            logger.LogWarning(
                "Weakness analysis queue is full; dropping job. UserId={UserId} Capacity={Capacity}",
                userId,
                queueCapacity);
            return false;
        }

        Interlocked.Increment(ref pendingJobCount);
        EnqueuedCounter.Add(1);
        return true;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "WeaknessAnalysisScheduler started. Mode=best-effort-in-memory Capacity={Capacity} TimeoutSeconds={TimeoutSeconds}",
            queueCapacity,
            (int)analysisTimeout.TotalSeconds);

        await foreach (var userId in queue.Reader.ReadAllAsync(stoppingToken))
        {
            if (!jobs.TryGetValue(userId, out var state))
                continue;

            if (!state.TryMarkRunning())
                continue;

            var waitMs = (DateTimeOffset.UtcNow - state.EnqueuedAtUtc).TotalMilliseconds;
            if (Interlocked.Decrement(ref pendingJobCount) < 0)
                Interlocked.Exchange(ref pendingJobCount, 0);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            timeoutCts.CancelAfter(analysisTimeout);

            var sw = Stopwatch.StartNew();
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IWeaknessAnalysisService>();
                await service.AnalyzeUserAsync(userId, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (OperationCanceledException ex) when (timeoutCts.IsCancellationRequested)
            {
                FailedCounter.Add(1);
                logger.LogWarning(
                    ex,
                    "Weakness analysis job timed out. UserId={UserId} TimeoutSeconds={TimeoutSeconds}",
                    userId,
                    (int)analysisTimeout.TotalSeconds);
            }
            catch (Exception ex)
            {
                FailedCounter.Add(1);
                logger.LogWarning(ex, "Weakness analysis queued job failed. UserId={UserId}", userId);
            }
            finally
            {
                ProcessingDurationHistogram.Record(sw.Elapsed.TotalMilliseconds);
                jobs.TryRemove(userId, out _);
                logger.LogDebug(
                    "Weakness analysis job completed. UserId={UserId} WaitMs={WaitMs:F1} PendingJobs={PendingJobs}",
                    userId,
                    waitMs,
                    GetPendingJobCount());
            }
        }

        logger.LogInformation(
            "WeaknessAnalysisScheduler stopped. Best-effort queue state was discarded; restart and replica ownership are local-only.");
    }

    private int pendingJobCount;

    private int GetPendingJobCount()
    {
        return Volatile.Read(ref pendingJobCount);
    }

    private double GetOldestPendingJobAgeSeconds()
    {
        var now = DateTimeOffset.UtcNow;
        var oldest = jobs.Values
            .Where(x => x.IsPending)
            .Select(x => (now - x.EnqueuedAtUtc).TotalSeconds)
            .DefaultIfEmpty(0d)
            .Min();

        return oldest < 0 ? 0 : oldest;
    }

    private sealed class WeaknessAnalysisJobState
    {
        private int status = (int)WeaknessAnalysisJobStatus.Pending;

        public WeaknessAnalysisJobState(DateTimeOffset enqueuedAtUtc)
        {
            EnqueuedAtUtc = enqueuedAtUtc;
        }

        public DateTimeOffset EnqueuedAtUtc { get; }

        public bool IsPending => Volatile.Read(ref status) == (int)WeaknessAnalysisJobStatus.Pending;

        public bool TryMarkRunning()
        {
            return Interlocked.CompareExchange(
                ref status,
                (int)WeaknessAnalysisJobStatus.Running,
                (int)WeaknessAnalysisJobStatus.Pending) == (int)WeaknessAnalysisJobStatus.Pending;
        }
    }

    private enum WeaknessAnalysisJobStatus
    {
        Pending = 0,
        Running = 1
    }
}

public sealed class WeaknessAnalysisDailyHostedService : BackgroundService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly IWeaknessAnalysisScheduler scheduler;
    private readonly ILogger<WeaknessAnalysisDailyHostedService> logger;

    public WeaknessAnalysisDailyHostedService(
        IServiceScopeFactory scopeFactory,
        IWeaknessAnalysisScheduler scheduler,
        ILogger<WeaknessAnalysisDailyHostedService> logger)
    {
        this.scopeFactory = scopeFactory;
        this.scheduler = scheduler;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timer = new PeriodicTimer(TimeSpan.FromHours(24));

        await RunForActiveUsersAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested &&
               await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunForActiveUsersAsync(stoppingToken);
        }
    }

    private async Task RunForActiveUsersAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MathLearning.Infrastructure.Persistance.ApiDbContext>();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var threshold = today.AddDays(-30);

            var activeUserIds = await db.UserProfiles
                .AsNoTracking()
                .Where(x => x.LastActivityDay != null && x.LastActivityDay >= threshold)
                .Select(x => x.UserId)
                .Distinct()
                .ToListAsync(ct);

            var rejectedCount = 0;
            foreach (var appUserId in activeUserIds)
            {
                var mapped = MathLearning.Application.Helpers.UserIdGuidMapper.FromIdentityUserId(appUserId);
                if (!scheduler.Enqueue(mapped))
                    rejectedCount++;
            }

            logger.LogInformation(
                "Daily weakness analysis enqueued for active users. Users={UserCount} Threshold={Threshold} Rejected={RejectedCount}",
                activeUserIds.Count,
                threshold,
                rejectedCount);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Graceful shutdown.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Daily weakness analysis enqueue failed.");
        }
    }
}
