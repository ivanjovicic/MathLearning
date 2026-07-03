using MathLearning.Infrastructure.Maintenance;

namespace MathLearning.Api.Services;

public sealed class IndexMaintenanceBackgroundService : BackgroundService
{
    private readonly ILogger<IndexMaintenanceBackgroundService> logger;
    private readonly IIndexMaintenanceService maintenanceService;

    public IndexMaintenanceBackgroundService(
        ILogger<IndexMaintenanceBackgroundService> logger,
        IIndexMaintenanceService maintenanceService)
    {
        this.logger = logger;
        this.maintenanceService = maintenanceService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Index Maintenance Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await WaitUntilScheduledTime(stoppingToken);

                if (stoppingToken.IsCancellationRequested)
                    break;

                logger.LogInformation("Running index maintenance");
                var report = await maintenanceService.RebuildCorruptedIndexesAsync(stoppingToken);
                LogMaintenanceReport(report);
                logger.LogInformation("Index maintenance completed");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Index maintenance failed");
            }
        }

        logger.LogInformation("Index Maintenance Service stopped");
    }

    private async Task WaitUntilScheduledTime(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var scheduledTime = new DateTime(now.Year, now.Month, now.Day, 3, 0, 0, DateTimeKind.Utc);

        if (now > scheduledTime)
            scheduledTime = scheduledTime.AddDays(1);

        var delay = scheduledTime - now;
        logger.LogInformation(
            "Next maintenance scheduled at {ScheduledTimeUtc} in {DelayHours:F1} hours",
            scheduledTime,
            delay.TotalHours);

        await Task.Delay(delay, cancellationToken);
    }

    private void LogMaintenanceReport(IndexMaintenanceReport report)
    {
        logger.LogInformation(
            "Index maintenance report. Bloated={BloatedCount} Unused={UnusedCount} Rebuilt={RebuiltCount} Errors={ErrorCount}",
            report.BloatedIndexes.Count,
            report.UnusedIndexes.Count,
            report.RebuiltIndexes.Count,
            report.Errors.Count);

        foreach (var index in report.RebuiltIndexes)
            logger.LogInformation("Rebuilt index {IndexName}", index);

        foreach (var index in report.UnusedIndexes.Take(5))
            logger.LogWarning("Unused index {IndexName}", index);

        foreach (var error in report.Errors)
            logger.LogError("Index maintenance item failed: {Error}", error);
    }
}
