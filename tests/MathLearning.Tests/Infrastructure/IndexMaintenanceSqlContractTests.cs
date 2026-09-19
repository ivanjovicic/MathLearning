namespace MathLearning.Tests.Infrastructure;

public sealed class IndexMaintenanceSqlContractTests
{
    [Fact]
    public void PgStatUserIndexesQueriesUseThePostgreSqlColumnNames()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "MathLearning.Infrastructure",
            "Maintenance",
            "IndexMaintenanceService.cs"));

        Assert.Equal(3, CountOccurrences(source, "FROM pg_stat_user_indexes"));
        Assert.Equal(2, CountOccurrences(source, "relname AS tablename"));
        Assert.Equal(2, CountOccurrences(source, "indexrelname AS indexname"));
        Assert.DoesNotContain("i.tablename", source, StringComparison.Ordinal);
        Assert.DoesNotContain("i.indexname", source, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string value, string search)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += search.Length;
        }

        return count;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MathLearning.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
