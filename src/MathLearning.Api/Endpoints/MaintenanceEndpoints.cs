using MathLearning.Infrastructure.Maintenance;
using Microsoft.AspNetCore.Authorization;

namespace MathLearning.Api.Endpoints;

public static class MaintenanceEndpoints
{
    public static void MapMaintenanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/maintenance")
                       .RequireAuthorization() // TODO: Add admin role check
                       .WithTags("Maintenance");

        // 🔧 REBUILD CORRUPTED INDEXES
        group.MapPost("/rebuild-indexes", async (
            IConfiguration config,
            ILogger<IndexMaintenanceService> logger) =>
        {
            var connectionString = config.GetConnectionString("Default");
            var service = new IndexMaintenanceService(connectionString!);

            logger.LogInformation("🔧 Manual index rebuild triggered");

            var report = await service.RebuildCorruptedIndexesAsync();

            logger.LogInformation($"✅ Rebuilt {report.RebuiltIndexes.Count} indexes");

            return Results.Ok(new
            {
                success = true,
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

        // 🔍 CHECK INDEX HEALTH
        group.MapGet("/index-health", async (
            IConfiguration config) =>
        {
            var connectionString = config.GetConnectionString("Default");
            var service = new IndexMaintenanceService(connectionString!);

            var healthInfo = await service.CheckIndexHealthAsync();

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

        // 📊 INDEX STATISTICS
        group.MapGet("/index-stats", async (
            IConfiguration config) =>
        {
            var connectionString = config.GetConnectionString("Default");
            var service = new IndexMaintenanceService(connectionString!);

            var report = await service.RebuildCorruptedIndexesAsync();

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
        .WithDescription("Get detailed statistics about index bloat and usage");
    }
}
