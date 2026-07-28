using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Api.Services;

public sealed class XpResetBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<XpResetBackgroundService> logger;

    public XpResetBackgroundService(
        IServiceScopeFactory serviceScopeFactory,
        TimeProvider timeProvider,
        ILogger<XpResetBackgroundService> logger)
    {
        this.serviceScopeFactory = serviceScopeFactory;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("XP reset background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<XpResetProcessor>();
                var result = await processor.RunOnceAsync(stoppingToken);

                logger.LogInformation(
                    "XP reset cycle finished. Status={Status} LockAcquired={LockAcquired} RowsAffected={RowsAffected} ElapsedMs={ElapsedMs:0.00}",
                    result.Status,
                    result.LockAcquired,
                    result.RowsAffected,
                    result.Elapsed.TotalMilliseconds);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while running XP reset cycle");
            }

            var delay = XpResetWindow.GetDelayUntilNextUtcBoundary(timeProvider.GetUtcNow());
            if (delay < TimeSpan.Zero)
            {
                delay = TimeSpan.Zero;
            }

            try
            {
                await Task.Delay(delay, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
