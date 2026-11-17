using MathLearning.Infrastructure.Maintenance;

namespace MathLearning.Tests.Tools;

public sealed class IndexMaintenanceToolOutputTests
{
    [Fact]
    public void NullBloatPercentageIsUnknownAndNeverHealthy()
    {
        var report = new IndexMaintenanceReport
        {
            BloatedIndexes =
            {
                new IndexBloatInfo
                {
                    IndexName = "IX_Unknown",
                    TableName = "Questions",
                    Size = "2 MB",
                    Scans = 0,
                    BloatPercentage = null,
                    Status = "HEALTHY"
                }
            }
        };

        var output = IndexMaintenanceToolOutput.FormatStatistics(report);

        Assert.Equal("UNKNOWN", IndexMaintenanceToolOutput.FormatBloatPercentage(null));
        Assert.Contains("UNKNOWN", output);
        Assert.DoesNotContain("HEALTHY", output);
        Assert.Contains("2 MB", output);
        Assert.Contains("0", output);
    }

    [Fact]
    public void ExplicitZeroBloatRemainsDistinctFromUnknown()
    {
        Assert.Equal("0.00", IndexMaintenanceToolOutput.FormatBloatPercentage(0m));
        Assert.Equal("UNKNOWN", IndexMaintenanceToolOutput.FormatBloatPercentage(null));
    }

    [Fact]
    public void ZeroObservedScansAreReportedAsUsageSeparateFromUnknownBloat()
    {
        var rows = new[]
        {
            new IndexHealthInfo
            {
                IndexName = "IX_Unused",
                TableName = "Questions",
                Size = "16 kB",
                Scans = 0,
                UsageStatus = "UNUSED",
                Status = "HEALTHY"
            }
        };

        var output = IndexMaintenanceToolOutput.FormatHealth(rows);

        Assert.Contains("Physical bloat: UNKNOWN", output);
        Assert.Contains("16 kB", output);
        Assert.Contains("0", output);
        Assert.Contains("UNUSED", output);
        Assert.DoesNotContain("HEALTHY", output);
    }

    [Fact]
    public void EmptyStatisticsDoNotDescribeMissingOrFilteredIndexesAsZeroBloat()
    {
        var output = IndexMaintenanceToolOutput.FormatStatistics(new IndexMaintenanceReport());

        Assert.Contains("No indexes matched", output);
        Assert.Contains("UNKNOWN", output);
        Assert.DoesNotContain("No bloated indexes found", output);
        Assert.DoesNotContain("HEALTHY", output);
    }

    [Fact]
    public void LegacyRebuildOutputStatesAnalyzeOnlyAndNoReindex()
    {
        var output = IndexMaintenanceToolOutput.FormatMaintenance(new IndexMaintenanceReport());

        Assert.Contains("ANALYZE only", output);
        Assert.Contains("REINDEX performed: no", output);
        Assert.DoesNotContain("Rebuilding bloated indexes", output);
    }
}
