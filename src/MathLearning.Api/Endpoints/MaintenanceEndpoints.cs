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
            logger.LogInformation("Manual index rebuild triggered");
            var report = await service.RebuildCorruptedIndexesAsync(cancellationToken);

            logger.LogInformation(
                "Manual index rebuild completed. Rebuilt={RebuiltIndexesCount} Errors={ErrorCount}",
                report.RebuiltIndexes.Count,
                report.Errors.Count);

            return Results.Ok(new
            {
                success = report.Errors.Count == 0,
                message = $"Rebuilt {report.RebuiltIndexes.Count} indexes",
                bloatedIndexes = report.BloatedIndexes.Count,
                unusedIndexes = report.UnusedIndexes.Count,
                rebuiltIndexes = report.RebuiltIndexes,
                errors = report.Errors,
                runAt = report.RunAt
            });
        })
        .WithName("RebuildIndexes")
        .WithDescription("Manually trigger index rebuild for bloated/corrupted indexes");

        group.MapGet("/index-health", async (
            IIndexMaintenanceService service,
            CancellationToken cancellationToken) =>
        {
            var healthInfo = await service.CheckIndexHealthAsync(cancellationToken);
            return Results.Ok(new
            {
                totalIndexes = healthInfo.Count,
                healthyIndexes = healthInfo.Count(i => i.Status == "HEALTHY"),
                unusedIndexes = healthInfo.Count(i => i.Status == "UNUSED"),
                lowUsageIndexes = healthInfo.Count(i => i.Status == "LOW_USAGE"),
                indexes = healthInfo
            });
        })
        .WithName("CheckIndexHealth")
        .WithDescription("Check health status of all database indexes");

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
                    status = i.BloatPercentage > 30 ? "NEEDS_REBUILD" :
                             i.BloatPercentage > 15 ? "WATCH" : "HEALTHY"
                }),
                unusedIndexes = report.UnusedIndexes
            });
        })
        .WithName("GetIndexStatistics")
        .WithDescription("Get read-only statistics about index bloat and usage");
    }
}
