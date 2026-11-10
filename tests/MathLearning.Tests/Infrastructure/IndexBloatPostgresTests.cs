using MathLearning.Infrastructure.Maintenance;
using MathLearning.Tests.Helpers;
using Npgsql;

namespace MathLearning.Tests.Infrastructure;

public sealed class IndexBloatPostgresTests
{
    [Fact]
    [Trait("Category", "PostgresProvider")]
    public async Task IndexBloat_ZeroForkDeltaIsNotEvidenceOfPhysicalHealth()
    {
        if (!IsValidationRequired()) return;

        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var connection = new NpgsqlConnection(database.DatabaseConnectionString);
        await connection.OpenAsync();
        await AssertPostgres16Async(connection);
        await CreateMaintenanceTablesAsync(connection);

        await using (var command = new NpgsqlCommand(
            """
            INSERT INTO "Questions" ("Id", "Value")
            SELECT n, md5(n::text) FROM generate_series(1, 50000) AS n;
            CREATE INDEX "IX_Questions_Value" ON "Questions" ("Value");
            """, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        await using (var command = new NpgsqlCommand(
            """
            SELECT pg_relation_size('"IX_Questions_Value"'::regclass),
                   pg_relation_size('"IX_Questions_Value"'::regclass)
                     - pg_relation_size('"IX_Questions_Value"'::regclass, 'main');
            """, connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            Assert.True(await reader.ReadAsync());
            Assert.True(reader.GetInt64(0) > 1048576);
            Assert.Equal(0, reader.GetInt64(1));
        }

        var service = new IndexMaintenanceService(database.DatabaseConnectionString);
        var report = await service.GetIndexStatisticsAsync();
        var index = Assert.Single(report.BloatedIndexes, i => i.IndexName == "IX_Questions_Value");
        Assert.Null(index.BloatPercentage);
        Assert.Equal("UNKNOWN", index.Status);
        Assert.Empty(report.RebuiltIndexes);

        var health = await service.CheckIndexHealthAsync();
        var row = Assert.Single(health, i => i.IndexName == "IX_Questions_Value");
        Assert.Equal("UNKNOWN", row.Status);
        Assert.Contains(row.UsageStatus, new[] { "UNUSED", "LOW_USAGE", "USED" });
    }

    [Fact]
    [Trait("Category", "PostgresProvider")]
    public async Task IndexMaintenance_AnalyzeStillRunsWithoutReindex()
    {
        if (!IsValidationRequired()) return;

        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var connection = new NpgsqlConnection(database.DatabaseConnectionString);
        await connection.OpenAsync();
        await AssertPostgres16Async(connection);
        await CreateMaintenanceTablesAsync(connection);

        await using (var command = new NpgsqlCommand(
            "INSERT INTO \"Questions\" (\"Id\", \"Value\") VALUES (1, 'sample');", connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        Assert.Equal(0, await CountQuestionStatisticsAsync(connection));
        var indexOidBefore = await GetQuestionIndexOidAsync(connection);
        var service = new IndexMaintenanceService(database.DatabaseConnectionString);
        var report = await service.RebuildCorruptedIndexesAsync();

        Assert.Empty(report.RebuiltIndexes);
        Assert.Empty(report.Errors);
        Assert.True(await CountQuestionStatisticsAsync(connection) > 0);
        Assert.Equal(indexOidBefore, await GetQuestionIndexOidAsync(connection));
    }

    private static bool IsValidationRequired() =>
        Environment.GetEnvironmentVariable("POSTGRES_PROVIDER_TESTS_REQUIRED") == "1";

    private static async Task AssertPostgres16Async(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("SHOW server_version_num;", connection);
        var version = int.Parse((string)(await command.ExecuteScalarAsync())!);
        Assert.InRange(version, 160000, 169999);
    }

    private static async Task CreateMaintenanceTablesAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            """
            CREATE TABLE "Questions" ("Id" integer PRIMARY KEY, "Value" text NOT NULL)
              WITH (autovacuum_enabled = false);
            CREATE TABLE "UserAnswers" (id integer);
            CREATE TABLE "UserQuestionStats" (id integer);
            CREATE TABLE "QuizSessions" (id integer);
            CREATE TABLE "Subtopics" (id integer);
            CREATE TABLE "Topics" (id integer);
            CREATE TABLE "Categories" (id integer);
            """, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> CountQuestionStatisticsAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM pg_stats WHERE schemaname = 'public' AND tablename = 'Questions' AND attname = 'Value';", connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<uint> GetQuestionIndexOidAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("SELECT 'public.\"Questions_pkey\"'::regclass::oid;", connection);
        return (uint)(await command.ExecuteScalarAsync())!;
    }
}
