using MathLearning.Infrastructure.Maintenance;

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
        Console.WriteLine("  check    Check health of all indexes");
        Console.WriteLine("  rebuild  Rebuild indexes above the configured bloat threshold");
        Console.WriteLine("  stats    Show read-only index statistics");
    }

    private static async Task CheckIndexHealth(IndexMaintenanceService service)
    {
        Console.WriteLine("Checking index health...\n");
        var healthInfo = await service.CheckIndexHealthAsync();

        Console.WriteLine($"Total Indexes: {healthInfo.Count}");
        Console.WriteLine($"Healthy: {healthInfo.Count(i => i.Status == "HEALTHY")}");
        Console.WriteLine($"Low Usage: {healthInfo.Count(i => i.Status == "LOW_USAGE")}");
        Console.WriteLine($"Unused: {healthInfo.Count(i => i.Status == "UNUSED")}");
        Console.WriteLine();

        Console.WriteLine(new string('-', 120));
        Console.WriteLine($"{"Table",-20} {"Index Name",-40} {"Size",-10} {"Scans",-10} {"Status",-15}");
        Console.WriteLine(new string('-', 120));

        foreach (var index in healthInfo.OrderBy(i => i.TableName).ThenBy(i => i.IndexName))
        {
            Console.WriteLine(
                $"{index.TableName,-20} {index.IndexName,-40} {index.Size,-10} {index.Scans,-10} {index.Status,-15}");
        }
    }

    private static async Task RebuildIndexes(IndexMaintenanceService service)
    {
        Console.WriteLine("Rebuilding bloated indexes...\n");
        var report = await service.RebuildCorruptedIndexesAsync();

        Console.WriteLine("Maintenance Report:");
        Console.WriteLine($"  Bloated Indexes: {report.BloatedIndexes.Count}");
        Console.WriteLine($"  Unused Indexes: {report.UnusedIndexes.Count}");
        Console.WriteLine($"  Rebuilt Indexes: {report.RebuiltIndexes.Count}");

        if (report.RebuiltIndexes.Count > 0)
        {
            Console.WriteLine("Rebuilt:");
            foreach (var index in report.RebuiltIndexes)
                Console.WriteLine($"  - {index}");
        }

        if (report.Errors.Count > 0)
        {
            Console.Error.WriteLine("Errors:");
            foreach (var error in report.Errors)
                Console.Error.WriteLine($"  - {error}");
        }
    }

    private static async Task ShowStatistics(IndexMaintenanceService service)
    {
        Console.WriteLine("Index Statistics (read-only)...\n");
        var report = await service.GetIndexStatisticsAsync();

        if (report.BloatedIndexes.Count == 0)
        {
            Console.WriteLine("No bloated indexes found.");
            return;
        }

        Console.WriteLine(new string('-', 120));
        Console.WriteLine(
            $"{"Index Name",-40} {"Table",-20} {"Size",-10} {"Bloat %",-10} {"Scans",-10} {"Status",-15}");
        Console.WriteLine(new string('-', 120));

        foreach (var index in report.BloatedIndexes.OrderByDescending(i => i.BloatPercentage))
        {
            var status = index.BloatPercentage > 30 ? "NEEDS_REBUILD" :
                         index.BloatPercentage > 15 ? "WATCH" : "HEALTHY";
            Console.WriteLine(
                $"{index.IndexName,-40} {index.TableName,-20} {index.Size,-10} " +
                $"{index.BloatPercentage,-10:F2} {index.Scans,-10} {status,-15}");
        }

        Console.WriteLine();
        Console.WriteLine($"Needs Rebuild: {report.BloatedIndexes.Count(i => i.BloatPercentage > 30)}");
        Console.WriteLine($"Watch: {report.BloatedIndexes.Count(i => i.BloatPercentage > 15 && i.BloatPercentage <= 30)}");
        Console.WriteLine($"Healthy: {report.BloatedIndexes.Count(i => i.BloatPercentage <= 15)}");
        Console.WriteLine($"Unused: {report.UnusedIndexes.Count}");
    }
}
