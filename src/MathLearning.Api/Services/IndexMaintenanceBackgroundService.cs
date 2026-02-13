using MathLearning.Infrastructure.Maintenance;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace MathLearning.Api.Services;

public class IndexMaintenanceBackgroundService : BackgroundService
{
    private readonly ILogger<IndexMaintenanceBackgroundService> _logger;
    private readonly IConfiguration _configuration;

    public IndexMaintenanceBackgroundService(
        ILogger<IndexMaintenanceBackgroundService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🔧 Index Maintenance Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Wait until 3 AM or configured time
                await WaitUntilScheduledTime(stoppingToken);

                if (stoppingToken.IsCancellationRequested)
                    break;

                _logger.LogInformation("🔍 Running index maintenance...");

                var connectionString = _configuration.GetConnectionString("Default");
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    _logger.LogWarning("⚠️ Index maintenance skipped: ConnectionStrings:Default is not configured.");
                    continue;
                }

                var service = new IndexMaintenanceService(connectionString);

                // Run maintenance
                var report = await service.RebuildCorruptedIndexesAsync();

                // Log report
                LogMaintenanceReport(report);

                _logger.LogInformation("✅ Index maintenance completed");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Index maintenance failed");
            }
        }

        _logger.LogInformation("🛑 Index Maintenance Service stopped");
    }

    private async Task WaitUntilScheduledTime(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var scheduledTime = new DateTime(now.Year, now.Month, now.Day, 3, 0, 0, DateTimeKind.Utc);

        // If already past 3 AM today, schedule for tomorrow
        if (now > scheduledTime)
        {
            scheduledTime = scheduledTime.AddDays(1);
        }

        var delay = scheduledTime - now;
        
        _logger.LogInformation($"⏰ Next maintenance scheduled at {scheduledTime:yyyy-MM-dd HH:mm:ss} UTC (in {delay.TotalHours:F1} hours)");

        await Task.Delay(delay, cancellationToken);
    }

    private void LogMaintenanceReport(IndexMaintenanceReport report)
    {
        _logger.LogInformation("📊 Maintenance Report:");
        _logger.LogInformation($"  - Bloated indexes: {report.BloatedIndexes.Count}");
        _logger.LogInformation($"  - Unused indexes: {report.UnusedIndexes.Count}");
        _logger.LogInformation($"  - Rebuilt indexes: {report.RebuiltIndexes.Count}");

        if (report.RebuiltIndexes.Any())
        {
            _logger.LogInformation("  ✅ Rebuilt indexes:");
            foreach (var index in report.RebuiltIndexes)
            {
                _logger.LogInformation($"    - {index}");
            }
        }

        if (report.UnusedIndexes.Any())
        {
            _logger.LogWarning("  ⚠️ Unused indexes (consider removing):");
            foreach (var index in report.UnusedIndexes.Take(5))
            {
                _logger.LogWarning($"    - {index}");
            }
        }

        if (report.Errors.Any())
        {
            _logger.LogError("  ❌ Errors:");
            foreach (var error in report.Errors)
            {
                _logger.LogError($"    - {error}");
            }
        }
    }
}
