using MathLearning.Api.Services;
using MathLearning.Application.Services;

namespace MathLearning.Api.Endpoints;

public static class MonitoringLogEndpoints
{
    public static void MapMonitoringLogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/monitoring")
            .RequireAuthorization(DesignTokenSecurity.AdminPolicy)
            .WithTags("Monitoring");

        group.MapGet("/logs", () =>
        {
            var logPath = ResolveLogFilePath();
            if (!File.Exists(logPath))
                return Results.Json(Array.Empty<string>());

            var lines = File.ReadLines(logPath).Reverse().Take(20).Reverse();
            return Results.Json(LogOutputRedactor.RedactLines(lines));
        })
        .WithName("GetMonitoringLogs")
        .WithDescription("Admin-only: recent Serilog file lines (redacted).");

        group.MapGet("/logs-advanced", (string? search, string? level) =>
        {
            var logPath = ResolveLogFilePath();
            if (!File.Exists(logPath))
                return Results.Json(Array.Empty<object>());

            var lines = File.ReadLines(logPath).Reverse().Take(200).Reverse();
            var entries = new List<object>();

            foreach (var line in lines)
            {
                var redactedLine = LogOutputRedactor.Redact(line);
                string? lvl = null;
                var idx1 = line.IndexOf('[');
                var idx2 = line.IndexOf(']');
                if (idx1 >= 0 && idx2 > idx1)
                    lvl = line.Substring(idx1 + 1, idx2 - idx1 - 1);

                if (!string.IsNullOrEmpty(level) && !string.Equals(lvl, level, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrEmpty(search) &&
                    !redactedLine.Contains(search, StringComparison.OrdinalIgnoreCase))
                    continue;

                entries.Add(new { Message = redactedLine, Level = lvl });
            }

            return Results.Json(entries);
        })
        .WithName("GetMonitoringLogsAdvanced")
        .WithDescription("Admin-only: filtered Serilog file entries (redacted).");
    }

    private static string ResolveLogFilePath() =>
        Path.Combine(AppContext.BaseDirectory, "Logs", "log.txt");
}
