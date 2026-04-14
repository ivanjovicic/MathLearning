using System.Threading.Channels;
using MathLearning.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Api.Services;

public interface IWeaknessAnalysisScheduler
{
    void Enqueue(Guid userId);
}

public sealed class WeaknessAnalysisScheduler : BackgroundService, IWeaknessAnalysisScheduler
{
    private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WeaknessAnalysisScheduler> _logger;

    public WeaknessAnalysisScheduler(
        IServiceScopeFactory scopeFactory,
        ILogger<WeaknessAnalysisScheduler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void Enqueue(Guid userId)
    {
        if (!_queue.Writer.TryWrite(userId))
            _logger.LogWarning("Failed to enqueue weakness analysis job. UserId={UserId}", userId);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WeaknessAnalysisScheduler started.");
        await foreach (var userId in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IWeaknessAnalysisService>();
                await service.AnalyzeUserAsync(userId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Weakness analysis queued job failed. UserId={UserId}", userId);
            }
        }

        _logger.LogInformation("WeaknessAnalysisScheduler stopped.");
    }
}

public sealed class WeaknessAnalysisDailyHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWeaknessAnalysisScheduler _scheduler;
    private readonly ILogger<WeaknessAnalysisDailyHostedService> _logger;

    public WeaknessAnalysisDailyHostedService(
        IServiceScopeFactory scopeFactory,
        IWeaknessAnalysisScheduler scheduler,
        ILogger<WeaknessAnalysisDailyHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _scheduler = scheduler;
        _logger = logger;
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
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MathLearning.Infrastructure.Persistance.ApiDbContext>();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var threshold = today.AddDays(-30);

            var activeUserIds = await db.UserProfiles
                .AsNoTracking()
                .Where(x => x.LastActivityDay != null && x.LastActivityDay >= threshold)
                .Select(x => x.UserId)
                .Distinct()
                .ToListAsync(ct);

            foreach (var appUserId in activeUserIds)
            {
                var mapped = MathLearning.Application.Helpers.UserIdGuidMapper.FromIdentityUserId(appUserId);
                _scheduler.Enqueue(mapped);
            }

            _logger.LogInformation(
                "Daily weakness analysis enqueued for active users. Users={UserCount} Threshold={Threshold}",
                activeUserIds.Count,
                threshold);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Graceful shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Daily weakness analysis enqueue failed.");
        }
    }
}
