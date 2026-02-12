using MathLearning.Infrastructure.Maintenance;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🔧 Index Maintenance Tool\n");

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
            Console.Error.WriteLine("❌ Missing connection string. Set CONNECTION_STRING (or ConnectionStrings__Default) environment variable.");
            Environment.Exit(2);
            return;
        }

        var service = new IndexMaintenanceService(connectionString);

        try
        {
            switch (args[0].ToLower())
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
                    Console.WriteLine($"❌ Unknown command: {args[0]}");
                    ShowUsage();
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    static void ShowUsage()
    {
        Console.WriteLine("Usage: IndexMaintenanceTool <command>");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  check    - Check health of all indexes");
        Console.WriteLine("  rebuild  - Rebuild bloated/corrupted indexes");
        Console.WriteLine("  stats    - Show detailed index statistics");
        Console.WriteLine();
        Console.WriteLine("Environment Variables:");
        Console.WriteLine("  CONNECTION_STRING - PostgreSQL connection string");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  IndexMaintenanceTool check");
        Console.WriteLine("  IndexMaintenanceTool rebuild");
        Console.WriteLine("  IndexMaintenanceTool stats");
    }

    static async Task CheckIndexHealth(IndexMaintenanceService service)
    {
        Console.WriteLine("🔍 Checking index health...\n");

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
            var statusColor = index.Status switch
            {
                "HEALTHY" => ConsoleColor.Green,
                "LOW_USAGE" => ConsoleColor.Yellow,
                "UNUSED" => ConsoleColor.Red,
                _ => ConsoleColor.White
            };

            Console.Write($"{index.TableName,-20} {index.IndexName,-40} {index.Size,-10} {index.Scans,-10} ");
            Console.ForegroundColor = statusColor;
            Console.WriteLine($"{index.Status,-15}");
            Console.ResetColor();
        }

        Console.WriteLine(new string('-', 120));
    }

    static async Task RebuildIndexes(IndexMaintenanceService service)
    {
        Console.WriteLine("🔧 Rebuilding bloated indexes...\n");

        var report = await service.RebuildCorruptedIndexesAsync();

        Console.WriteLine("📊 Maintenance Report:");
        Console.WriteLine($"  Bloated Indexes: {report.BloatedIndexes.Count}");
        Console.WriteLine($"  Unused Indexes: {report.UnusedIndexes.Count}");
        Console.WriteLine($"  Rebuilt Indexes: {report.RebuiltIndexes.Count}");
        Console.WriteLine();

        if (report.BloatedIndexes.Any())
        {
            Console.WriteLine("📋 Bloated Indexes:");
            foreach (var index in report.BloatedIndexes.OrderByDescending(i => i.BloatPercentage))
            {
                var statusEmoji = index.BloatPercentage > 30 ? "❌" : 
                                  index.BloatPercentage > 15 ? "⚠️" : "✅";
                Console.WriteLine($"  {statusEmoji} {index.IndexName} - {index.BloatPercentage}% bloat ({index.Size})");
            }
            Console.WriteLine();
        }

        if (report.RebuiltIndexes.Any())
        {
            Console.WriteLine("✅ Rebuilt Indexes:");
            foreach (var index in report.RebuiltIndexes)
            {
                Console.WriteLine($"  ✓ {index}");
            }
            Console.WriteLine();
        }

        if (report.UnusedIndexes.Any())
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("⚠️ Unused Indexes (consider removing):");
            foreach (var index in report.UnusedIndexes.Take(5))
            {
                Console.WriteLine($"  - {index}");
            }
            if (report.UnusedIndexes.Count > 5)
            {
                Console.WriteLine($"  ... and {report.UnusedIndexes.Count - 5} more");
            }
            Console.ResetColor();
            Console.WriteLine();
        }

        if (report.Errors.Any())
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("❌ Errors:");
            foreach (var error in report.Errors)
            {
                Console.WriteLine($"  - {error}");
            }
            Console.ResetColor();
        }
    }

    static async Task ShowStatistics(IndexMaintenanceService service)
    {
        Console.WriteLine("📊 Index Statistics...\n");

        var report = await service.RebuildCorruptedIndexesAsync();

        if (!report.BloatedIndexes.Any())
        {
            Console.WriteLine("✅ No bloated indexes found!");
            return;
        }

        Console.WriteLine(new string('-', 120));
        Console.WriteLine($"{"Index Name",-40} {"Table",-20} {"Size",-10} {"Bloat %",-10} {"Scans",-10} {"Status",-15}");
        Console.WriteLine(new string('-', 120));

        foreach (var index in report.BloatedIndexes.OrderByDescending(i => i.BloatPercentage))
        {
            var status = index.BloatPercentage > 30 ? "NEEDS_REBUILD" :
                        index.BloatPercentage > 15 ? "WATCH" : "HEALTHY";

            var statusColor = status switch
            {
                "NEEDS_REBUILD" => ConsoleColor.Red,
                "WATCH" => ConsoleColor.Yellow,
                "HEALTHY" => ConsoleColor.Green,
                _ => ConsoleColor.White
            };

            Console.Write($"{index.IndexName,-40} {index.TableName,-20} {index.Size,-10} {index.BloatPercentage,-10:F2} {index.Scans,-10} ");
            Console.ForegroundColor = statusColor;
            Console.WriteLine($"{status,-15}");
            Console.ResetColor();
        }

        Console.WriteLine(new string('-', 120));

        // Summary
        var needsRebuild = report.BloatedIndexes.Count(i => i.BloatPercentage > 30);
        var watch = report.BloatedIndexes.Count(i => i.BloatPercentage > 15 && i.BloatPercentage <= 30);
        var healthy = report.BloatedIndexes.Count(i => i.BloatPercentage <= 15);

        Console.WriteLine();
        Console.WriteLine("Summary:");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  ❌ Needs Rebuild: {needsRebuild}");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  ⚠️ Watch: {watch}");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  ✅ Healthy: {healthy}");
        Console.ResetColor();
    }
}
