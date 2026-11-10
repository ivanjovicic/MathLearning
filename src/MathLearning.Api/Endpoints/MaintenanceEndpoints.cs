using MathLearning.Application.Services;
using MathLearning.Infrastructure.Maintenance;

namespace MathLearning.Api.Endpoints;

public static class MaintenanceEndpoints
{
    public static void MapMaintenanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/maintenance")
            .RequireAuthorization(DesignTokenSecurity.AdminPolicy)
            .WithTags("Maintenance");

        group.MapPost("/rebuild-indexes", async (
            IIndexMaintenanceService service,
            ILogger<IndexMaintenanceService> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation("Manual index statistics update triggered; automatic REINDEX disabled");
            var report = await service.RebuildCorruptedIndexesAsync(cancellationToken);

            logger.LogInformation(
                "Index statistics update completed. Rebuilt={RebuiltIndexesCount} Errors={ErrorCount}",
                report.RebuiltIndexes.Count,
                report.Errors.Count);

            return Results.Ok(new
            {
                success = report.Errors.Count == 0,
                message = "ANALYZE completed; index bloat is UNKNOWN and no indexes were rebuilt",
                bloatedIndexes = 0,
                unknownBloatIndexes = report.BloatedIndexes.Count,
                automaticReindexEnabled = false,
                unusedIndexes = report.UnusedIndexes.Count,
                rebuiltIndexes = report.RebuiltIndexes,
                errors = report.Errors,
                runAt = report.RunAt
            });
        })
        .WithName("RebuildIndexes")
        .WithDescription("Legacy route: update table statistics with ANALYZE; automatic index rebuild is disabled");

        group.MapGet("/index-health", async (
            IIndexMaintenanceService service,
            CancellationToken cancellationToken) =>
        {
            var healthInfo = await service.CheckIndexHealthAsync(cancellationToken);
            return Results.Ok(new
            {
                totalIndexes = healthInfo.Count,
                healthyIndexes = healthInfo.Count(i => i.Status == "HEALTHY"),
                unknownIndexes = healthInfo.Count(i => i.Status == "UNKNOWN"),
                unusedIndexes = healthInfo.Count(i => i.UsageStatus == "UNUSED"),
                lowUsageIndexes = healthInfo.Count(i => i.UsageStatus == "LOW_USAGE"),
                indexes = healthInfo
            });
        })
        .WithName("CheckIndexHealth")
        .WithDescription("Read index size and usage; physical index health is UNKNOWN without a verified measurement");

        group.MapGet("/index-stats", async (
            IIndexMaintenanceService service,
            CancellationToken cancellationToken) =>
        {
            var report = await service.GetIndexStatisticsAsync(cancellationToken);
            return Results.Ok(new
            {
                bloatedIndexes = report.BloatedIndexes.Select(i => new
                {
                    i.IndexName,
                    i.TableName,
                    i.Size,
                    i.BloatPercentage,
                    i.Scans,
                    i.Status
                }),
                unusedIndexes = report.UnusedIndexes
            });
        })
        .WithName("GetIndexStatistics")
        .WithDescription("Get read-only index size and usage; bloat percentage is UNKNOWN without verified measurement");
    }
}
