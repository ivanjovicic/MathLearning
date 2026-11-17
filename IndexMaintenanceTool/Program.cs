using MathLearning.Infrastructure.Maintenance;
using System.Globalization;
using System.Text;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("Index Maintenance Tool\n");

        if (args.Length == 0)
        {
            ShowUsage();
            return;
        }

        var connectionString =
            Environment.GetEnvironmentVariable("CONNECTION_STRING")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine(
                "Missing connection string. Set CONNECTION_STRING or ConnectionStrings__Default.");
            Environment.ExitCode = 2;
            return;
        }

        var service = new IndexMaintenanceService(connectionString);

        try
        {
            switch (args[0].ToLowerInvariant())
            {
                case "check":
                    await CheckIndexHealth(service);
                    break;
                case "rebuild":
                    await RebuildIndexes(service);
                    break;
                case "stats":
                    await ShowStatistics(service);
                    break;
                default:
                    Console.Error.WriteLine($"Unknown command: {args[0]}");
                    ShowUsage();
                    Environment.ExitCode = 2;
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Maintenance command failed: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    private static void ShowUsage()
    {
        Console.WriteLine("Usage: IndexMaintenanceTool <command>");
        Console.WriteLine("  check    Show index size, scan usage, and unknown physical bloat status");
        Console.WriteLine("  rebuild  Legacy command: run ANALYZE only; automatic REINDEX is disabled");
        Console.WriteLine("  stats    Show read-only index size and usage statistics; bloat may be UNKNOWN");
    }

    private static async Task CheckIndexHealth(IndexMaintenanceService service)
    {
        var healthInfo = await service.CheckIndexHealthAsync();
        Console.Write(IndexMaintenanceToolOutput.FormatHealth(healthInfo));
    }

    private static async Task RebuildIndexes(IndexMaintenanceService service)
    {
        var report = await service.RebuildCorruptedIndexesAsync();
        Console.Write(IndexMaintenanceToolOutput.FormatMaintenance(report));

        if (report.Errors.Count > 0)
        {
            Console.Error.WriteLine("Errors:");
            foreach (var error in report.Errors)
                Console.Error.WriteLine($"  - {error}");
        }
    }

    private static async Task ShowStatistics(IndexMaintenanceService service)
    {
        var report = await service.GetIndexStatisticsAsync();
        Console.Write(IndexMaintenanceToolOutput.FormatStatistics(report));
    }
}

public static class IndexMaintenanceToolOutput
{
    public static string FormatBloatPercentage(decimal? bloatPercentage) =>
        bloatPercentage?.ToString("F2", CultureInfo.InvariantCulture) ?? "UNKNOWN";

    public static string FormatHealth(IReadOnlyCollection<IndexHealthInfo> healthInfo)
    {
        var output = new StringBuilder();
        output.AppendLine("Index observations (physical bloat is not measured):");
        output.AppendLine();
        output.AppendLine($"Total Indexes: {healthInfo.Count}");
        output.AppendLine($"Physical bloat: UNKNOWN for all {healthInfo.Count} index(es)");
        output.AppendLine($"Usage UNUSED (zero observed scans): {CountUsage(healthInfo, "UNUSED")}");
        output.AppendLine($"Usage LOW_USAGE: {CountUsage(healthInfo, "LOW_USAGE")}");
        output.AppendLine($"Usage USED: {CountUsage(healthInfo, "USED")}");
        output.AppendLine($"Usage UNKNOWN: {CountUnknownUsage(healthInfo)}");
        output.AppendLine();
        output.AppendLine(new string('-', 120));
        output.AppendLine(
            $"{"Table",-20} {"Index Name",-40} {"Index Size",-14} {"Scans",-10} {"Physical Bloat",-15} {"Usage",-12}");
        output.AppendLine(new string('-', 120));

        foreach (var index in healthInfo.OrderBy(i => i.TableName).ThenBy(i => i.IndexName))
        {
            output.AppendLine(
                $"{index.TableName,-20} {index.IndexName,-40} {index.Size,-14} {index.Scans,-10} " +
                $"{"UNKNOWN",-15} {FormatUsageStatus(index.UsageStatus),-12}");
        }

        if (healthInfo.Count == 0)
        {
            output.AppendLine("No indexes were returned by PostgreSQL's user-index statistics query.");
        }

        return output.ToString();
    }

    public static string FormatMaintenance(IndexMaintenanceReport report)
    {
        var output = new StringBuilder();
        output.AppendLine("Legacy maintenance command: ANALYZE only; automatic REINDEX is disabled.");
        output.AppendLine();
        output.AppendLine("Maintenance Report:");
        output.AppendLine($"  Indexes returned by size-filtered statistics: {report.BloatedIndexes.Count}");
        output.AppendLine($"  Unused indexes (zero observed scans): {report.UnusedIndexes.Count}");
        output.AppendLine("  REINDEX performed: no");
        return output.ToString();
    }

    public static string FormatStatistics(IndexMaintenanceReport report)
    {
        var output = new StringBuilder();
        output.AppendLine("Index Statistics (read-only)...");
        output.AppendLine("Size and scan counts are observations; physical bloat is not inferred from them.");
        output.AppendLine();

        var measuredCount = report.BloatedIndexes.Count(i => i.BloatPercentage.HasValue);
        var unknownCount = report.BloatedIndexes.Count - measuredCount;
        output.AppendLine($"Indexes returned by size-filtered statistics: {report.BloatedIndexes.Count}");
        output.AppendLine($"Bloat measurements: measured {measuredCount}, UNKNOWN {unknownCount}");
        output.AppendLine($"Unused indexes (zero observed scans): {report.UnusedIndexes.Count}");

        if (report.BloatedIndexes.Count == 0)
        {
            output.AppendLine("No indexes matched the size-filtered statistics query; physical bloat is UNKNOWN, not 0%.");
            return output.ToString();
        }

        output.AppendLine(new string('-', 120));
        output.AppendLine(
            $"{"Index Name",-40} {"Table",-20} {"Index Size",-14} {"Scans",-10} {"Bloat %",-12} {"Bloat Status",-14}");
        output.AppendLine(new string('-', 120));

        foreach (var index in report.BloatedIndexes.OrderBy(i => i.IndexName))
        {
            var bloatStatus = index.BloatPercentage.HasValue ? "MEASURED" : "UNKNOWN";
            output.AppendLine(
                $"{index.IndexName,-40} {index.TableName,-20} {index.Size,-14} {index.Scans,-10} " +
                $"{FormatBloatPercentage(index.BloatPercentage),-12} {bloatStatus,-14}");
        }

        return output.ToString();
    }

    private static int CountUsage(IReadOnlyCollection<IndexHealthInfo> healthInfo, string usageStatus) =>
        healthInfo.Count(i => string.Equals(i.UsageStatus, usageStatus, StringComparison.OrdinalIgnoreCase));

    private static int CountUnknownUsage(IReadOnlyCollection<IndexHealthInfo> healthInfo) =>
        healthInfo.Count(i => string.IsNullOrWhiteSpace(i.UsageStatus));

    private static string FormatUsageStatus(string? usageStatus) =>
        string.IsNullOrWhiteSpace(usageStatus) ? "UNKNOWN" : usageStatus;
}
