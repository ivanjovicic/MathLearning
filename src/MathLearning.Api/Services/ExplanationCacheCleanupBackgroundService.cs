using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MathLearning.Application.Services;

namespace MathLearning.Api.Services;

public sealed class ExplanationCacheCleanupBackgroundService : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);
    private const int CleanupBatchSize = 100;

    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<ExplanationCacheCleanupBackgroundService> logger;

    public ExplanationCacheCleanupBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ExplanationCacheCleanupBackgroundService> logger)
    {
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CleanupInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RunCleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Explanation cache cleanup sweep failed.");
            }
        }
    }

    private async Task RunCleanupAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IExplanationCacheService>();
        await cache.CleanupExpiredEntriesAsync(CleanupBatchSize, ct);
    }
}
