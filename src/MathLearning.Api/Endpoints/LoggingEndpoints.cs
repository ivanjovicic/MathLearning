using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace MathLearning.Api.Endpoints;

public static class LoggingEndpoints
{
    public static void MapLoggingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/logs")
                       .RequireAuthorization() // TODO: Add admin role check
                       .WithTags("Logging");

        // 📋 GET RECENT LOGS
        group.MapGet("/recent", async (
            ApiDbContext db,
            string? level = null,
            int limit = 100) =>
        {
            var query = db.ApplicationLogs.AsQueryable();

            if (!string.IsNullOrEmpty(level))
            {
                query = query.Where(l => l.Level == level.ToUpper());
            }

            var logs = await query
                .OrderByDescending(l => l.Timestamp)
                .Take(limit)
                .Select(l => new
                {
                    l.Id,
                    l.Timestamp,
                    l.Level,
                    l.Message,
                    l.Exception,
                    l.RequestPath,
                    l.UserName,
                    l.MachineName
                })
                .ToListAsync();

            return Results.Ok(logs);
        })
        .WithName("GetRecentLogs")
        .WithDescription("Get recent application logs");

        // 📊 GET LOGS BY LEVEL
        group.MapGet("/level/{level}", async (
            string level,
            ApiDbContext db,
            int limit = 50) =>
        {
            var logs = await db.ApplicationLogs
                .Where(l => l.Level == level.ToUpper())
                .OrderByDescending(l => l.Timestamp)
                .Take(limit)
                .ToListAsync();

            return Results.Ok(logs);
        })
        .WithName("GetLogsByLevel")
        .WithDescription("Get logs by severity level (INFO, WARNING, ERROR, FATAL)");

        // 🔍 SEARCH LOGS
        group.MapGet("/search", async (
            ApiDbContext db,
            string? query = null,
            DateTime? from = null,
            DateTime? to = null,
            string? level = null,
            int limit = 100) =>
        {
            var logsQuery = db.ApplicationLogs.AsQueryable();

            if (!string.IsNullOrEmpty(query))
            {
                logsQuery = logsQuery.Where(l => 
                    l.Message.Contains(query) || 
                    (l.Exception != null && l.Exception.Contains(query)));
            }

            if (from.HasValue)
            {
                logsQuery = logsQuery.Where(l => l.Timestamp >= from.Value);
            }

            if (to.HasValue)
            {
                logsQuery = logsQuery.Where(l => l.Timestamp <= to.Value);
            }

            if (!string.IsNullOrEmpty(level))
            {
                logsQuery = logsQuery.Where(l => l.Level == level.ToUpper());
            }

            var logs = await logsQuery
                .OrderByDescending(l => l.Timestamp)
                .Take(limit)
                .ToListAsync();

            return Results.Ok(logs);
        })
        .WithName("SearchLogs")
        .WithDescription("Search logs with filters");

        // 📊 GET LOG STATISTICS
        group.MapGet("/stats", async (ApiDbContext db) =>
        {
            var last24Hours = DateTime.UtcNow.AddHours(-24);

            var stats = await db.ApplicationLogs
                .Where(l => l.Timestamp >= last24Hours)
                .GroupBy(l => l.Level)
                .Select(g => new
                {
                    Level = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            var totalLogs = await db.ApplicationLogs.CountAsync();
            var oldestLog = await db.ApplicationLogs
                .OrderBy(l => l.Timestamp)
                .Select(l => l.Timestamp)
                .FirstOrDefaultAsync();

            return Results.Ok(new
            {
                totalLogs,
                oldestLog,
                last24Hours = stats,
                summary = new
                {
                    info = stats.FirstOrDefault(s => s.Level == "Information")?.Count ?? 0,
                    warning = stats.FirstOrDefault(s => s.Level == "Warning")?.Count ?? 0,
                    error = stats.FirstOrDefault(s => s.Level == "Error")?.Count ?? 0,
                    fatal = stats.FirstOrDefault(s => s.Level == "Fatal")?.Count ?? 0
                }
            });
        })
        .WithName("GetLogStatistics")
        .WithDescription("Get logging statistics");

        // 🗑️ DELETE OLD LOGS
        group.MapDelete("/cleanup", async (
            ApiDbContext db,
            int daysToKeep = 30) =>
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);

            var oldLogs = await db.ApplicationLogs
                .Where(l => l.Timestamp < cutoffDate)
                .ToListAsync();

            db.ApplicationLogs.RemoveRange(oldLogs);
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                message = $"Deleted logs older than {daysToKeep} days",
                deletedCount = oldLogs.Count,
                cutoffDate
            });
        })
        .WithName("CleanupOldLogs")
        .WithDescription("Delete logs older than specified days");

        // 📝 GET LOG DETAIL
        group.MapGet("/{id}", async (
            long id,
            ApiDbContext db) =>
        {
            var log = await db.ApplicationLogs.FindAsync(id);

            if (log == null)
                return Results.NotFound(new { error = "Log not found" });

            return Results.Ok(log);
        })
        .WithName("GetLogDetail")
        .WithDescription("Get detailed log entry");

        // 🔥 GET ERROR LOGS (last 24h)
        group.MapGet("/errors/recent", async (
            ApiDbContext db,
            int limit = 50) =>
        {
            var last24Hours = DateTime.UtcNow.AddHours(-24);

            var errors = await db.ApplicationLogs
                .Where(l => l.Timestamp >= last24Hours && 
                           (l.Level == "Error" || l.Level == "Fatal"))
                .OrderByDescending(l => l.Timestamp)
                .Take(limit)
                .ToListAsync();

            return Results.Ok(errors);
        })
        .WithName("GetRecentErrors")
        .WithDescription("Get recent error and fatal logs");

        // 📊 LOG LEVEL DISTRIBUTION
        group.MapGet("/distribution", async (ApiDbContext db) =>
        {
            var distribution = await db.ApplicationLogs
                .GroupBy(l => l.Level)
                .Select(g => new
                {
                    level = g.Key,
                    count = g.Count(),
                    percentage = Math.Round(100.0 * g.Count() / db.ApplicationLogs.Count(), 2)
                })
                .OrderByDescending(x => x.count)
                .ToListAsync();

            return Results.Ok(distribution);
        })
        .WithName("GetLogDistribution")
        .WithDescription("Get log level distribution");
    }
}
