using MathLearning.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MathLearning.Infrastructure.Services.Sync;

public sealed class SyncDeadLetterRedriveBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly IOptions<SyncOptions> options;
    private readonly ILogger<SyncDeadLetterRedriveBackgroundService> logger;

    public SyncDeadLetterRedriveBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<SyncOptions> options,
        ILogger<SyncDeadLetterRedriveBackgroundService> logger)
    {
        this.scopeFactory = scopeFactory;
        this.options = options;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.EnableDeadLetterRedriveWorker)
        {
            logger.LogInformation("Sync dead-letter redrive worker is disabled.");
            return;
        }

        logger.LogInformation(
            "Sync dead-letter redrive worker started. IntervalSeconds={IntervalSeconds} BatchSize={BatchSize}",
            options.Value.DeadLetterRedriveIntervalSeconds,
            options.Value.DeadLetterRedriveBatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var syncAdminService = scope.ServiceProvider.GetRequiredService<ISyncAdminService>();
                var result = await syncAdminService.RedriveDeadLettersAsync(
                    options.Value.DeadLetterRedriveBatchSize,
                    includeExhausted: false,
                    actorUserId: "sync-redrive-worker",
                    stoppingToken);

                if (result.Attempted > 0)
                {
                    logger.LogInformation(
                        "Sync dead-letter redrive batch completed. Attempted={Attempted} Succeeded={Succeeded} Failed={Failed} Exhausted={Exhausted}",
                        result.Attempted,
                        result.Succeeded,
                        result.Failed,
                        result.Exhausted);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Sync dead-letter redrive worker iteration failed.");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(Math.Max(5, options.Value.DeadLetterRedriveIntervalSeconds)),
                stoppingToken);
        }
    }
}
