using System.Data;
using MathLearning.Api.Services;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;

namespace MathLearning.Tests.Database;

public sealed class DatabaseSchemaValidationTests
{
    [Fact]
    [Trait("Category", "DatabaseSchema")]
    public async Task CriticalColumnsExist()
    {
        if (!IsValidationRequired())
        {
            return;
        }

        await using var db = CreateDbContext();

        await AssertColumnExistsAsync(db, "SyncDeadLetter", "Status");
        await AssertColumnExistsAsync(db, "SyncDeadLetter", "RetryCount");
        await AssertColumnExistsAsync(db, "SyncDeadLetter", "LastFailedAtUtc");
        await AssertColumnExistsAsync(db, "SyncDeadLetter", "LastRedriveAttemptAtUtc");
        await AssertColumnExistsAsync(db, "SyncDeadLetter", "ResolvedAtUtc");
        await AssertColumnExistsAsync(db, "SyncDeadLetter", "ResolutionNote");
        await AssertColumnExistsAsync(db, "SyncDeadLetter", "SyncEventLogId");
        await AssertColumnExistsAsync(db, "SyncDeadLetter", "PayloadHash");

        await AssertColumnExistsAsync(db, "SyncEventLog", "PayloadHash");

        await AssertColumnExistsAsync(db, "Questions", "CurrentDraftId");
        await AssertColumnExistsAsync(db, "Questions", "CurrentVersionNumber");
        await AssertColumnExistsAsync(db, "Questions", "HintFull");
        await AssertColumnExistsAsync(db, "Questions", "PublishState");
        await AssertColumnExistsAsync(db, "Questions", "PublishedAtUtc");
        await AssertColumnExistsAsync(db, "Questions", "PublishedByUserId");
        await AssertColumnExistsAsync(db, "Questions", "ExplanationFormat");
        await AssertColumnExistsAsync(db, "Questions", "HintFormat");
        await AssertColumnExistsAsync(db, "Questions", "CorrectOptionId");

        await AssertColumnExistsAsync(db, "QuestionSteps", "HintFormat");
        await AssertColumnExistsAsync(db, "QuestionSteps", "HintRenderMode");
        await AssertColumnExistsAsync(db, "QuestionSteps", "TextFormat");
        await AssertColumnExistsAsync(db, "QuestionSteps", "TextRenderMode");
        await AssertColumnExistsAsync(db, "QuestionSteps", "SemanticsAltText");

        await AssertColumnExistsAsync(db, "Options", "QuestionId");
        await AssertColumnExistsAsync(db, "Options", "IsCorrect");
        await AssertColumnExistsAsync(db, "Options", "RenderMode");
        await AssertColumnExistsAsync(db, "Options", "SemanticsAltText");
        await AssertColumnExistsAsync(db, "Options", "TextFormat");

        await AssertColumnExistsAsync(db, "UserProfiles", "UserId");
        await AssertColumnExistsAsync(db, "UserProfiles", "Username");
        await AssertColumnExistsAsync(db, "UserProfiles", "DisplayName");

        await AssertColumnExistsAsync(db, "user_settings", "UserId");
        await AssertColumnExistsAsync(db, "user_settings", "Language");
        await AssertColumnExistsAsync(db, "user_settings", "Theme");

        await AssertColumnExistsAsync(db, "AspNetUsers", "Id");
        await AssertColumnExistsAsync(db, "AspNetUsers", "UserName");
    }

    [Fact]
    [Trait("Category", "DatabaseSchema")]
    public async Task CriticalIndexesExist()
    {
        if (!IsValidationRequired())
        {
            return;
        }

        await using var db = CreateDbContext();

        await AssertIndexExistsAsync(db, "SyncDeadLetter", "IX_SyncDeadLetter_Status_LastFailedAtUtc");
        await AssertIndexExistsAsync(db, "SyncDeadLetter", "UX_SyncDeadLetter_User_Device_OperationId");
        await AssertIndexExistsAsync(db, "SyncEventLog", "UX_SyncEventLog_User_Device_OperationId");
        await AssertIndexExistsAsync(db, "SyncEventLog", "UX_SyncEventLog_User_Device_Sequence");
        await AssertIndexExistsAsync(db, "UserAnswers", "UX_UserAnswers_User_Device_SyncOperationId");
        await AssertIndexExistsAsync(db, "Questions", "IX_Questions_CurrentDraftId");
        await AssertIndexExistsAsync(db, "Questions", "IX_Questions_PublishState");
        await AssertIndexExistsAsync(db, "Questions", "IX_Questions_CorrectOptionId");
        await AssertIndexExistsAsync(db, "QuestionSteps", "IX_QuestionSteps_Question_Index");
        await AssertIndexExistsAsync(db, "Options", "IX_Options_IsCorrect");
        await AssertIndexExistsAsync(db, "UserProfiles", "UX_UserProfiles_Username");
        await AssertIndexExistsAsync(db, "user_settings", "UX_UserSettings_UserId");
    }

    [Fact]
    [Trait("Category", "DatabaseSchema")]
    public async Task CriticalConstraintsExist()
    {
        if (!IsValidationRequired())
        {
            return;
        }

        await using var db = CreateDbContext();

        await AssertConstraintExistsAsync(db, "Questions", "FK_Questions_Options_CorrectOptionId");
        await AssertConstraintExistsAsync(db, "QuestionSteps", "FK_QuestionSteps_Questions_QuestionId");
        await AssertConstraintExistsAsync(db, "Options", "FK_Options_Questions_QuestionId");
        await AssertConstraintExistsAsync(db, "UserProfiles", "FK_UserProfiles_AspNetUsers_UserId");
    }

    [Fact]
    [Trait("Category", "DatabaseSchema")]
    public void CosmeticsMigrationUsesConstraintIntrospectionForHistoricalDrift()
    {
        var options = new DbContextOptionsBuilder<ApiDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata_only;Username=test;Password=test")
            .Options;

        using var db = new ApiDbContext(options);
        var migrator = db.GetService<IMigrator>();

        var script = migrator.GenerateScript(
            fromMigration: "20260309091241_AddCosmeticSystem",
            toMigration: "20260624133144_AlignCosmeticsMobileDataModel",
            options: MigrationsSqlGenerationOptions.Idempotent);

        Assert.Contains("pg_constraint", script, StringComparison.Ordinal);
        Assert.Contains("cardinality(c.conkey) = 1", script, StringComparison.Ordinal);
        Assert.Contains("ALTER TABLE %I DROP CONSTRAINT %I", script, StringComparison.Ordinal);
        Assert.DoesNotContain("FK_user_avatar_configs_UserProfiles_UserId", script, StringComparison.Ordinal);
        Assert.DoesNotContain("PK_user_avatar_configs", script, StringComparison.Ordinal);
        Assert.DoesNotContain("PK_user_cosmetic_inventory", script, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "DatabaseSchema")]
    public async Task MigrationHistoryMatchesCompiledModel()
    {
        if (!IsValidationRequired())
        {
            return;
        }

        await using var db = CreateDbContext();
        var guard = new DatabaseSchemaVersionGuard(NullLogger<DatabaseSchemaVersionGuard>.Instance);

        var status = await guard.CheckAsync(db);

        Assert.True(
            status.IsSchemaReady,
            $"Expected database schema to match the compiled model. {status.FailureMessage}");
    }

    private static bool IsValidationRequired()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable("SCHEMA_VALIDATION_REQUIRED"),
            "1",
            StringComparison.Ordinal);
    }

    private static ApiDbContext CreateDbContext()
    {
        var connectionString = Environment.GetEnvironmentVariable("DATABASE_SCHEMA_VALIDATION_CONNECTION_STRING")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "DATABASE_SCHEMA_VALIDATION_CONNECTION_STRING or ConnectionStrings__Default must be set for schema validation tests.");
        }

        var options = new DbContextOptionsBuilder<ApiDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .Options;

        return new ApiDbContext(options);
    }

    private static async Task AssertColumnExistsAsync(ApiDbContext db, string tableName, string columnName)
    {
        await AssertRecordExistsAsync(
            db,
            @"
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = @tableName
                  AND column_name = @columnName;",
            $"Expected column '{tableName}.{columnName}' to exist.",
            ("tableName", tableName),
            ("columnName", columnName));
    }

    private static async Task AssertIndexExistsAsync(ApiDbContext db, string tableName, string indexName)
    {
        await AssertRecordExistsAsync(
            db,
            @"
                SELECT 1
                FROM pg_indexes
                WHERE schemaname = 'public'
                  AND tablename = @tableName
                  AND indexname = @indexName;",
            $"Expected index '{indexName}' on table '{tableName}' to exist.",
            ("tableName", tableName),
            ("indexName", indexName));
    }

    private static async Task AssertConstraintExistsAsync(ApiDbContext db, string tableName, string constraintName)
    {
        await AssertRecordExistsAsync(
            db,
            @"
                SELECT 1
                FROM pg_constraint constraint_info
                INNER JOIN pg_class table_info ON table_info.oid = constraint_info.conrelid
                INNER JOIN pg_namespace schema_info ON schema_info.oid = table_info.relnamespace
                WHERE schema_info.nspname = 'public'
                  AND table_info.relname = @tableName
                  AND constraint_info.conname = @constraintName;",
            $"Expected constraint '{constraintName}' on table '{tableName}' to exist.",
            ("tableName", tableName),
            ("constraintName", constraintName));
    }

    private static async Task AssertRecordExistsAsync(
        ApiDbContext db,
        string sql,
        string failureMessage,
        params (string Name, object Value)[] parameters)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        using var command = connection.CreateCommand();
        command.CommandText = sql;

        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        var result = await command.ExecuteScalarAsync();
        Assert.True(result is not null and not DBNull, failureMessage);
    }
}
