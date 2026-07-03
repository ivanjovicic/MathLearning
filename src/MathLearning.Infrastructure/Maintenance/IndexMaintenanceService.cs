using Npgsql;

namespace MathLearning.Infrastructure.Maintenance;

public interface IIndexMaintenanceService
{
    Task<IndexMaintenanceReport> RebuildCorruptedIndexesAsync(CancellationToken cancellationToken = default);

    Task<IndexMaintenanceReport> GetIndexStatisticsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IndexHealthInfo>> CheckIndexHealthAsync(CancellationToken cancellationToken = default);
}

public sealed class IndexMaintenanceService : IIndexMaintenanceService
{
    private readonly string connectionString;
    private readonly SemaphoreSlim rebuildGate = new(1, 1);

    public IndexMaintenanceService(string connectionString)
    {
        this.connectionString = connectionString;
    }

    public async Task<IndexMaintenanceReport> RebuildCorruptedIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        await rebuildGate.WaitAsync(cancellationToken);
        try
        {
            var report = await GetIndexStatisticsCoreAsync(cancellationToken);
            await using var connection = await OpenConnectionAsync(cancellationToken);

            foreach (var index in report.BloatedIndexes.Where(i => i.BloatPercentage > 30))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await ReindexAsync(connection, index.IndexName, cancellationToken);
                    report.RebuiltIndexes.Add(index.IndexName);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    report.Errors.Add($"Failed to rebuild {index.IndexName}: {ex.Message}");
                }
            }

            await UpdateStatisticsAsync(connection, cancellationToken);
            return report;
        }
        finally
        {
            rebuildGate.Release();
        }
    }

    public Task<IndexMaintenanceReport> GetIndexStatisticsAsync(
        CancellationToken cancellationToken = default) =>
        GetIndexStatisticsCoreAsync(cancellationToken);

    public async Task<IReadOnlyList<IndexHealthInfo>> CheckIndexHealthAsync(
        CancellationToken cancellationToken = default)
    {
        var healthInfo = new List<IndexHealthInfo>();
        await using var connection = await OpenConnectionAsync(cancellationToken);

        const string query = """
            SELECT
                i.schemaname,
                i.tablename,
                i.indexname,
                pg_size_pretty(pg_relation_size(i.indexrelid)) as size,
                i.idx_scan as scans,
                i.idx_tup_read as tuples_read,
                i.idx_tup_fetch as tuples_fetched,
                CASE
                    WHEN i.idx_scan = 0 THEN 'UNUSED'
                    WHEN i.idx_scan < 100 THEN 'LOW_USAGE'
                    ELSE 'HEALTHY'
                END as status
            FROM pg_stat_user_indexes i
            WHERE i.schemaname = 'public'
            ORDER BY pg_relation_size(i.indexrelid) DESC;
            """;

        await using var command = new NpgsqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            healthInfo.Add(new IndexHealthInfo
            {
                SchemaName = reader.GetString(0),
                TableName = reader.GetString(1),
                IndexName = reader.GetString(2),
                Size = reader.GetString(3),
                Scans = reader.GetInt64(4),
                TuplesRead = reader.GetInt64(5),
                TuplesFetched = reader.GetInt64(6),
                Status = reader.GetString(7)
            });
        }

        return healthInfo;
    }

    private async Task<IndexMaintenanceReport> GetIndexStatisticsCoreAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        return new IndexMaintenanceReport
        {
            BloatedIndexes = await DetectBloatedIndexesAsync(connection, cancellationToken),
            UnusedIndexes = await DetectUnusedIndexesAsync(connection, cancellationToken)
        };
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Index maintenance database is not configured.");
        }

        var connection = new NpgsqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private static async Task<List<IndexBloatInfo>> DetectBloatedIndexesAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string query = """
            SELECT
                schemaname,
                tablename,
                indexname,
                pg_size_pretty(pg_relation_size(indexrelid)) as size,
                idx_scan as scans,
                idx_tup_read as tuples_read,
                idx_tup_fetch as tuples_fetched,
                CASE
                    WHEN pg_relation_size(indexrelid) > 0
                    THEN ROUND(100 * (pg_relation_size(indexrelid) -
                         pg_relation_size(indexrelid, 'main')) /
                         pg_relation_size(indexrelid)::numeric, 2)
                    ELSE 0
                END as bloat_percentage
            FROM pg_stat_user_indexes
            WHERE schemaname = 'public'
              AND pg_relation_size(indexrelid) > 1048576
            ORDER BY pg_relation_size(indexrelid) DESC;
            """;

        var bloatedIndexes = new List<IndexBloatInfo>();
        await using var command = new NpgsqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            bloatedIndexes.Add(new IndexBloatInfo
            {
                SchemaName = reader.GetString(0),
                TableName = reader.GetString(1),
                IndexName = reader.GetString(2),
                Size = reader.GetString(3),
                Scans = reader.GetInt64(4),
                TuplesRead = reader.GetInt64(5),
                TuplesFetched = reader.GetInt64(6),
                BloatPercentage = reader.GetDecimal(7)
            });
        }

        return bloatedIndexes;
    }

    private static async Task<List<string>> DetectUnusedIndexesAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string query = """
            SELECT
                schemaname || '.' || indexrelname as full_index_name
            FROM pg_stat_user_indexes
            WHERE schemaname = 'public'
              AND idx_scan = 0
              AND indexrelname NOT LIKE 'pg_toast%'
              AND indexrelname NOT LIKE '%_pkey'
            ORDER BY pg_relation_size(indexrelid) DESC;
            """;

        var unusedIndexes = new List<string>();
        await using var command = new NpgsqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            unusedIndexes.Add(reader.GetString(0));
        }

        return unusedIndexes;
    }

    private static async Task ReindexAsync(
        NpgsqlConnection connection,
        string indexName,
        CancellationToken cancellationToken)
    {
        var quotedIndexName = new NpgsqlCommandBuilder().QuoteIdentifier(indexName);
        var reindexQuery = $"REINDEX INDEX CONCURRENTLY {quotedIndexName};";

        await using var command = new NpgsqlCommand(reindexQuery, connection)
        {
            CommandTimeout = 300
        };
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpdateStatisticsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var tables = new[]
        {
            "Questions",
            "UserAnswers",
            "UserQuestionStats",
            "QuizSessions",
            "Subtopics",
            "Topics",
            "Categories"
        };

        foreach (var table in tables)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var quotedTable = new NpgsqlCommandBuilder().QuoteIdentifier(table);
            await using var command = new NpgsqlCommand($"ANALYZE {quotedTable};", connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}

public class IndexMaintenanceReport
{
    public List<IndexBloatInfo> BloatedIndexes { get; set; } = new();
    public List<string> UnusedIndexes { get; set; } = new();
    public List<string> RebuiltIndexes { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public DateTime RunAt { get; set; } = DateTime.UtcNow;
}

public class IndexBloatInfo
{
    public string SchemaName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string IndexName { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public long Scans { get; set; }
    public long TuplesRead { get; set; }
    public long TuplesFetched { get; set; }
    public decimal BloatPercentage { get; set; }
}

public class IndexHealthInfo
{
    public string SchemaName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string IndexName { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public long Scans { get; set; }
    public long TuplesRead { get; set; }
    public long TuplesFetched { get; set; }
    public string Status { get; set; } = string.Empty;
}
