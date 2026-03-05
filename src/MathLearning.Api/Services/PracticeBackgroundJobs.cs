using MathLearning.Application.Services;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Hangfire;

namespace MathLearning.Api.Services;

public interface IPracticeBackgroundJobs
{
    Task EnqueuePostSessionJobsAsync(string userId, CancellationToken ct = default);
}

public sealed class PracticeBackgroundJobs : IPracticeBackgroundJobs
{
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly IPracticeAnalyticsUpdater _analyticsUpdater;
    private readonly IAdaptiveAnalyticsService _analytics;
    private readonly ILogger<PracticeBackgroundJobs> _logger;

    public PracticeBackgroundJobs(
        IBackgroundJobClient backgroundJobs,
        IPracticeAnalyticsUpdater analyticsUpdater,
        IAdaptiveAnalyticsService analytics,
        ILogger<PracticeBackgroundJobs> logger)
    {
        _backgroundJobs = backgroundJobs;
        _analyticsUpdater = analyticsUpdater;
        _analytics = analytics;
        _logger = logger;
    }

    public async Task EnqueuePostSessionJobsAsync(string userId, CancellationToken ct = default)
    {
        _backgroundJobs.Enqueue<IPracticeHangfireJobs>(x => x.RecomputeWeaknessForUserJob(userId));
        _backgroundJobs.Enqueue<IPracticeHangfireJobs>(x => x.RefreshAdaptivePathJob(userId));
        _backgroundJobs.Enqueue<IPracticeHangfireJobs>(x => x.GenerateRecommendationsJob(userId));

        _analytics.TrackEvent("practice_jobs_enqueued", userId, new
        {
            jobs = new[]
            {
                "RecomputeWeaknessForUserJob",
                "RefreshAdaptivePathJob",
                "GenerateRecommendationsJob"
            }
        });

        _logger.LogInformation(
            "Practice post-session jobs enqueued (hosted-service fallback). UserId={UserId}",
            userId);

        await Task.CompletedTask;
    }
}

public interface IPracticeHangfireJobs
{
    Task RecomputeWeaknessForUserJob(string userId);
    Task RefreshAdaptivePathJob(string userId);
    Task GenerateRecommendationsJob(string userId);
    Task DailyAggregationJob();
}

public sealed class PracticeHangfireJobs : IPracticeHangfireJobs
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PracticeHangfireJobs> _logger;

    public PracticeHangfireJobs(
        IServiceScopeFactory scopeFactory,
        ILogger<PracticeHangfireJobs> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task RecomputeWeaknessForUserJob(string userId)
    {
        using var scope = _scopeFactory.CreateScope();
        var updater = scope.ServiceProvider.GetRequiredService<IPracticeAnalyticsUpdater>();
        await updater.TriggerWeaknessRecomputeAsync(userId);
    }

    public async Task RefreshAdaptivePathJob(string userId)
    {
        using var scope = _scopeFactory.CreateScope();
        var facade = scope.ServiceProvider.GetRequiredService<AdaptiveApiFacade>();
        await facade.GetAdaptivePathAsync(userId, CancellationToken.None);
    }

    public async Task GenerateRecommendationsJob(string userId)
    {
        using var scope = _scopeFactory.CreateScope();
        var facade = scope.ServiceProvider.GetRequiredService<AdaptiveApiFacade>();
        await facade.GetAdaptiveRecommendationsAsync(userId, CancellationToken.None);
    }

    public async Task DailyAggregationJob()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var backgroundJobs = scope.ServiceProvider.GetRequiredService<IBackgroundJobClient>();

        var threshold = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30);
        var activeUsers = await db.UserProfiles
            .AsNoTracking()
            .Where(x => x.LastActivityDay != null && x.LastActivityDay >= threshold)
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync();

        foreach (var userId in activeUsers)
        {
            backgroundJobs.Enqueue<IPracticeHangfireJobs>(x => x.RecomputeWeaknessForUserJob(userId));
            backgroundJobs.Enqueue<IPracticeHangfireJobs>(x => x.RefreshAdaptivePathJob(userId));
            backgroundJobs.Enqueue<IPracticeHangfireJobs>(x => x.GenerateRecommendationsJob(userId));
        }

        _logger.LogInformation(
            "Hangfire daily aggregation queued. ActiveUsers={ActiveUsers} Threshold={Threshold}",
            activeUsers.Count,
            threshold);
    }
}
