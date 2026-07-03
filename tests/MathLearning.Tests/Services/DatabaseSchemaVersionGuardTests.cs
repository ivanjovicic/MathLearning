using MathLearning.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace MathLearning.Tests.Services;

public sealed class DatabaseSchemaVersionGuardTests
{
    [Theory]
    [InlineData("AutoMigrate", DatabaseStartupMode.AutoMigrate)]
    [InlineData("automigrate", DatabaseStartupMode.AutoMigrate)]
    [InlineData("ValidateExact", DatabaseStartupMode.ValidateExact)]
    [InlineData("validateexact", DatabaseStartupMode.ValidateExact)]
    [InlineData("Skip", DatabaseStartupMode.Skip)]
    [InlineData("skip", DatabaseStartupMode.Skip)]
    public void ResolveStartupMode_ValidConfiguredValueOverridesEnvironment(
        string configured,
        DatabaseStartupMode expected)
    {
        var actual = DatabaseSchemaVersionGuard.ResolveStartupMode(
            Environment("Production"),
            Configuration(configured));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("Development", DatabaseStartupMode.AutoMigrate)]
    [InlineData("Test", DatabaseStartupMode.Skip)]
    [InlineData("Production", DatabaseStartupMode.ValidateExact)]
    [InlineData("Staging", DatabaseStartupMode.ValidateExact)]
    public void ResolveStartupMode_MissingConfigurationUsesSafeEnvironmentDefault(
        string environmentName,
        DatabaseStartupMode expected)
    {
        var actual = DatabaseSchemaVersionGuard.ResolveStartupMode(
            Environment(environmentName),
            Configuration(null));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("not-a-mode")]
    [InlineData("  ")]
    public void ResolveStartupMode_InvalidConfigurationFallsBackToEnvironment(string configured)
    {
        var actual = DatabaseSchemaVersionGuard.ResolveStartupMode(
            Environment("Production"),
            Configuration(configured));

        Assert.Equal(DatabaseStartupMode.ValidateExact, actual);
    }

    [Fact]
    public void NotCheckedStatus_HasSafeNonReadyDefaults()
    {
        var status = DatabaseSchemaStatus.NotChecked;

        Assert.Equal("NotChecked", status.Status);
        Assert.False(status.IsSchemaReady);
        Assert.Null(status.LatestCodeMigration);
        Assert.Null(status.LatestAppliedMigration);
        Assert.Empty(status.PendingMigrations);
        Assert.Empty(status.UnknownAppliedMigrations);
        Assert.Equal(0, status.PendingMigrationsCount);
        Assert.Equal(0, status.UnknownAppliedMigrationsCount);
        Assert.Null(status.FailureMessage);
        Assert.Equal(DateTime.MinValue, status.CheckedAtUtc);
    }

    [Fact]
    public void FailedStatus_DefaultsMissingCollectionsAndRecordsFailure()
    {
        var before = DateTime.UtcNow;

        var status = DatabaseSchemaStatus.Failed("database unavailable");

        var after = DateTime.UtcNow;
        Assert.Equal("Failed", status.Status);
        Assert.False(status.IsSchemaReady);
        Assert.Equal("database unavailable", status.FailureMessage);
        Assert.Empty(status.PendingMigrations);
        Assert.Empty(status.UnknownAppliedMigrations);
        Assert.InRange(status.CheckedAtUtc, before, after);
    }

    [Fact]
    public void FailedStatus_PreservesMigrationEvidenceAndCounts()
    {
        var status = DatabaseSchemaStatus.Failed(
            failureMessage: "schema mismatch",
            latestCodeMigration: "202607030002_Code",
            latestAppliedMigration: "202607030001_Applied",
            pendingMigrations: new[] { "pending-1", "pending-2" },
            unknownAppliedMigrations: new[] { "unknown-1" });

        Assert.Equal("202607030002_Code", status.LatestCodeMigration);
        Assert.Equal("202607030001_Applied", status.LatestAppliedMigration);
        Assert.Equal(2, status.PendingMigrationsCount);
        Assert.Equal(1, status.UnknownAppliedMigrationsCount);
        Assert.Equal(new[] { "pending-1", "pending-2" }, status.PendingMigrations);
        Assert.Equal(new[] { "unknown-1" }, status.UnknownAppliedMigrations);
    }

    [Fact]
    public void DatabaseSchemaState_UpdateReplacesCurrentSnapshot()
    {
        var state = new DatabaseSchemaState();
        var replacement = DatabaseSchemaStatus.Failed("mismatch");

        Assert.Same(DatabaseSchemaStatus.NotChecked, state.Current);

        state.Update(replacement);

        Assert.Same(replacement, state.Current);
    }

    [Fact]
    public void CreateMismatchException_ValidateExactIncludesDeploymentGuidanceAndEvidence()
    {
        var guard = CreateGuard();
        var inner = new InvalidOperationException("provider failure");
        var status = DatabaseSchemaStatus.Failed(
            failureMessage: "Pending: migration-b",
            latestCodeMigration: "migration-b",
            latestAppliedMigration: "migration-a",
            pendingMigrations: new[] { "migration-b" });

        var error = guard.CreateMismatchException(
            DatabaseStartupMode.ValidateExact,
            "Production",
            status,
            inner);

        Assert.Same(inner, error.InnerException);
        Assert.Contains("Production", error.Message, StringComparison.Ordinal);
        Assert.Contains("LatestApplied=migration-a", error.Message, StringComparison.Ordinal);
        Assert.Contains("LatestCode=migration-b", error.Message, StringComparison.Ordinal);
        Assert.Contains("Pending=1", error.Message, StringComparison.Ordinal);
        Assert.Contains("UnknownApplied=0", error.Message, StringComparison.Ordinal);
        Assert.Contains("Details=Pending: migration-b", error.Message, StringComparison.Ordinal);
        Assert.Contains("Apply the reviewed migration script", error.Message, StringComparison.Ordinal);
        Assert.Contains("Startup migrations are disabled", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateMismatchException_AutoMigrateUsesLocalRepairGuidance()
    {
        var guard = CreateGuard();
        var status = DatabaseSchemaStatus.Failed(
            failureMessage: "UnknownApplied: migration-x",
            unknownAppliedMigrations: new[] { "migration-x" });

        var error = guard.CreateMismatchException(
            DatabaseStartupMode.AutoMigrate,
            "Development",
            status);

        Assert.Contains("drop-dev-db.ps1", error.Message, StringComparison.Ordinal);
        Assert.Contains("explicit repair migration", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Startup migrations are disabled", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateMismatchException_OmitsFailureDetails_WhenMissing()
    {
        var guard = CreateGuard();
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

    [Fact]
    public void CreateMismatchException_WithoutMigrationNamesUsesNonePlaceholders()
    {
        var guard = CreateGuard();
        var status = new DatabaseSchemaStatus(
            Status: "Mismatch",
            IsSchemaReady: false,
            LatestCodeMigration: null,
            LatestAppliedMigration: null,
            PendingMigrations: Array.Empty<string>(),
            UnknownAppliedMigrations: Array.Empty<string>(),
            FailureMessage: null,
            CheckedAtUtc: DateTime.UtcNow);

        var error = guard.CreateMismatchException(
            DatabaseStartupMode.ValidateExact,
            "Staging",
            status);

        Assert.Contains("LatestApplied=<none>", error.Message, StringComparison.Ordinal);
        Assert.Contains("LatestCode=<none>", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Details=", error.Message, StringComparison.Ordinal);
    }

    private static DatabaseSchemaVersionGuard CreateGuard() =>
        new(NullLogger<DatabaseSchemaVersionGuard>.Instance);

    private static IHostEnvironment Environment(string name)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(x => x.EnvironmentName).Returns(name);
        return environment.Object;
    }

    private static IConfiguration Configuration(string? startupMode)
    {
        var values = new Dictionary<string, string?>();
        if (startupMode is not null)
            values["Database:StartupMode"] = startupMode;

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
