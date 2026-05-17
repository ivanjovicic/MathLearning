using MathLearning.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace MathLearning.Tests.Services;

public class DatabaseSchemaVersionGuardTests
{
    [Fact]
    public void CreateMismatchException_IncludesFailureDetails_WhenPresent()
    {
        var guard = new DatabaseSchemaVersionGuard(NullLogger<DatabaseSchemaVersionGuard>.Instance);
        var status = new DatabaseSchemaStatus(
            Status: "Mismatch",
            IsSchemaReady: false,
            LatestCodeMigration: "20260504143000_RepairSchemaForInconsistentDatabases",
            LatestAppliedMigration: "20260504143000_RepairSchemaForInconsistentDatabases",
            PendingMigrations: Array.Empty<string>(),
            UnknownAppliedMigrations: new[] { "20260101000000_PreviousLocalOnlyMigration" },
            FailureMessage: "UnknownApplied: 20260101000000_PreviousLocalOnlyMigration",
            CheckedAtUtc: DateTime.UtcNow);

        var exception = guard.CreateMismatchException(
            DatabaseStartupMode.AutoMigrate,
            "Development",
            status);

        Assert.Contains("UnknownApplied=1;", exception.Message, StringComparison.Ordinal);
        Assert.Contains(
            "Details=UnknownApplied: 20260101000000_PreviousLocalOnlyMigration;",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CreateMismatchException_OmitsFailureDetails_WhenMissing()
    {
        var guard = new DatabaseSchemaVersionGuard(NullLogger<DatabaseSchemaVersionGuard>.Instance);
        var status = new DatabaseSchemaStatus(
            Status: "Mismatch",
            IsSchemaReady: false,
            LatestCodeMigration: "20260504143000_RepairSchemaForInconsistentDatabases",
            LatestAppliedMigration: "20260504143000_RepairSchemaForInconsistentDatabases",
            PendingMigrations: Array.Empty<string>(),
            UnknownAppliedMigrations: new[] { "20260101000000_PreviousLocalOnlyMigration" },
            FailureMessage: " ",
            CheckedAtUtc: DateTime.UtcNow);

        var exception = guard.CreateMismatchException(
            DatabaseStartupMode.AutoMigrate,
            "Development",
            status);

        Assert.DoesNotContain("Details=", exception.Message, StringComparison.Ordinal);
        Assert.Contains(
            "Use scripts/db/drop-dev-db.ps1 for a local reset",
            exception.Message,
            StringComparison.Ordinal);
    }
}
