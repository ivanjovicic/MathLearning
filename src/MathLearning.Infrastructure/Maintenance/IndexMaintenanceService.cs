using Npgsql;

namespace MathLearning.Infrastructure.Maintenance;

public class IndexMaintenanceService
{
    private readonly string _connectionString;

    public IndexMaintenanceService(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// Detektuje i rekreira pokvarene indexe
    /// </summary>
    public async Task<IndexMaintenanceReport> RebuildCorruptedIndexesAsync()
    {
        var report = new IndexMaintenanceReport();
        
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        // 1. Proveri bloat (fragmentacija) indexa
        var bloatedIndexes = await DetectBloatedIndexesAsync(connection);
        report.BloatedIndexes = bloatedIndexes;

        // 2. Proveri nekorišćene indexe
        var unusedIndexes = await DetectUnusedIndexesAsync(connection);
        report.UnusedIndexes = unusedIndexes;

        // 3. Rekreiraj bloated indexe
        foreach (var index in bloatedIndexes.Where(i => i.BloatPercentage > 30))
        {
            try
            {
                await ReindexAsync(connection, index.IndexName);
                report.RebuiltIndexes.Add(index.IndexName);
            }
            catch (Exception ex)
            {
                report.Errors.Add($"Failed to rebuild {index.IndexName}: {ex.Message}");
            }
        }

        // 4. Update statistics
        await UpdateStatisticsAsync(connection);

        return report;
    }

    /// <summary>
    /// Detektuje fragmentirane (bloated) indexe
    /// </summary>
    private async Task<List<IndexBloatInfo>> DetectBloatedIndexesAsync(NpgsqlConnection connection)
    {
        var query = @"
            SELECT 
                schemaname,
                tablename,
                indexname,
                pg_size_pretty(pg_relation_size(indexrelid)) as size,
                idx_scan as scans,
                idx_tup_read as tuples_read,
                idx_tup_fetch as tuples_fetched,
                -- Estimate bloat percentage
                CASE 
                    WHEN pg_relation_size(indexrelid) > 0 
                    THEN ROUND(100 * (pg_relation_size(indexrelid) - 
                         pg_relation_size(indexrelid, 'main')) / 
                         pg_relation_size(indexrelid)::numeric, 2)
                    ELSE 0 
                END as bloat_percentage
            FROM pg_stat_user_indexes
            WHERE schemaname = 'public'
              AND pg_relation_size(indexrelid) > 1048576 -- > 1MB
            ORDER BY pg_relation_size(indexrelid) DESC;";

        var bloatedIndexes = new List<IndexBloatInfo>();

        await using var command = new NpgsqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
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

    /// <summary>
    /// Detektuje nekorišćene indexe
    /// </summary>
    private async Task<List<string>> DetectUnusedIndexesAsync(NpgsqlConnection connection)
    {
        var query = @"
            SELECT 
                schemaname || '.' || indexrelname as full_index_name
            FROM pg_stat_user_indexes
            WHERE schemaname = 'public'
              AND idx_scan = 0
              AND indexrelname NOT LIKE 'pg_toast%'
              AND indexrelname NOT LIKE '%_pkey'  -- Keep primary keys
            ORDER BY pg_relation_size(indexrelid) DESC;";

        var unusedIndexes = new List<string>();

        await using var command = new NpgsqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            unusedIndexes.Add(reader.GetString(0));
        }

        return unusedIndexes;
    }

    /// <summary>
    /// Rekreira specifičan index
    /// </summary>
    private async Task ReindexAsync(NpgsqlConnection connection, string indexName)
    {
        var reindexQuery = $"REINDEX INDEX CONCURRENTLY \"{indexName}\";";
        
        await using var command = new NpgsqlCommand(reindexQuery, connection);
        command.CommandTimeout = 300; // 5 minutes
        
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Ažurira statistike za query optimizer
    /// </summary>
    private async Task UpdateStatisticsAsync(NpgsqlConnection connection)
    {
        var tables = new[] { "Questions", "UserAnswers", "UserQuestionStats", 
                            "QuizSessions", "Subtopics", "Topics", "Categories" };

        foreach (var table in tables)
        {
            var analyzeQuery = $"ANALYZE \"{table}\";";
            await using var command = new NpgsqlCommand(analyzeQuery, connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Proverava zdravlje svih indexa
    /// </summary>
    public async Task<List<IndexHealthInfo>> CheckIndexHealthAsync()
    {
        var healthInfo = new List<IndexHealthInfo>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        var query = @"
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
            ORDER BY pg_relation_size(i.indexrelid) DESC;";

        await using var command = new NpgsqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
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
}

// DTOs
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
