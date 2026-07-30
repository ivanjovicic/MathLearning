using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MathLearning.Infrastructure.Services.Sync;

public sealed class SyncRetentionCleanupBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly IOptions<SyncOptions> options;
    private readonly ILogger<SyncRetentionCleanupBackgroundService> logger;

    public SyncRetentionCleanupBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<SyncOptions> options,
        ILogger<SyncRetentionCleanupBackgroundService> logger)
    {
        this.scopeFactory = scopeFactory;
        this.options = options;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.EnableRetentionCleanupWorker)
        {
            logger.LogInformation("Sync retention cleanup worker is disabled.");
            return;
        }

        var delay = TimeSpan.FromSeconds(Math.Max(60, options.Value.RetentionCleanupIntervalSeconds));
        logger.LogInformation(
            "Sync retention cleanup worker started. IntervalSeconds={IntervalSeconds} BatchSize={BatchSize}",
            options.Value.RetentionCleanupIntervalSeconds,
            options.Value.RetentionBatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var retention = scope.ServiceProvider.GetRequiredService<SyncRetentionService>();
                await retention.CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Sync retention cleanup iteration failed.");
            }

            await Task.Delay(delay, stoppingToken);
        }
    }
}
