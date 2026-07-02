using MathLearning.Api.Endpoints;
using MathLearning.Domain.Entities;
using MathLearning.Tests.Helpers;

namespace MathLearning.Tests.Endpoints;

public sealed class DailyRunCosmeticsSettlementTests
{
    [Theory]
    [InlineData("dailyrun", true)]
    [InlineData("daily_run", true)]
    [InlineData("dailyRun", false)]
    [InlineData("season", false)]
    [InlineData("", false)]
    public void IsDailyRunSource_RecognizesOnlyNormalizedDailyRunValues(string source, bool expected)
    {
        Assert.Equal(expected, DailyRunCosmeticsSettlement.IsDailyRunSource(source));
    }

    [Theory]
    [InlineData(null, "operation-1", "operation-1")]
    [InlineData("", "operation-2", "operation-2")]
    [InlineData("   ", "operation-3", "operation-3")]
    [InlineData("  transaction-1  ", "operation-4", "transaction-1")]
    public void ResolveTransactionId_UsesTrimmedTransactionOrFallsBackToOperation(
        string? requestTransactionId,
        string operationId,
        string expected)
    {
        Assert.Equal(
            expected,
            DailyRunCosmeticsSettlement.ResolveTransactionId(requestTransactionId, operationId));
    }

    [Fact]
    public async Task ResolveFragmentGrantAsync_WhenChestClaimMissing_ReturnsEligibilityError()
    {
        await using var db = TestDbContextFactory.Create();

        var result = await DailyRunCosmeticsSettlement.ResolveFragmentGrantAsync(
            db,
            "user-1",
            "missing-transaction",
            CancellationToken.None);

        Assert.NotNull(result.Error);
        Assert.Equal(string.Empty, result.FragmentName);
        Assert.Equal(0, result.Copies);
    }

    [Fact]
    public async Task ResolveFragmentGrantAsync_WhenSeasonSettlementMissing_ReturnsEligibilityError()
    {
        await using var db = TestDbContextFactory.Create();
        await SeedChestClaimAsync(db, "user-1", "tx-1", "Comet Frame Fragment", 2);

        var result = await DailyRunCosmeticsSettlement.ResolveFragmentGrantAsync(
            db,
            "user-1",
            "tx-1",
            CancellationToken.None);

        Assert.NotNull(result.Error);
        Assert.Equal(string.Empty, result.FragmentName);
        Assert.Equal(0, result.Copies);
    }

    [Fact]
    public async Task ResolveFragmentGrantAsync_WhenOnlyOtherUserSettled_DoesNotAuthorizeGrant()
    {
        await using var db = TestDbContextFactory.Create();
        await SeedChestClaimAsync(db, "user-1", "shared-tx", "Comet Frame Fragment", 2);
        db.UserSeasonDailyRunClaims.Add(new UserSeasonDailyRunClaim
        {
            UserId = "user-2",
            SeasonId = 1,
            DailyRunTransactionId = "shared-tx",
            AwardedXp = 30
        });
        await db.SaveChangesAsync();

        var result = await DailyRunCosmeticsSettlement.ResolveFragmentGrantAsync(
            db,
            "user-1",
            "shared-tx",
            CancellationToken.None);

        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task ResolveFragmentGrantAsync_WhenFragmentPayloadMissing_ReturnsInvalidPayloadError()
    {
        await using var db = TestDbContextFactory.Create();
        await SeedChestClaimAsync(db, "user-1", "tx-empty", "   ", 2);
        await SeedSeasonSettlementAsync(db, "user-1", "tx-empty");

        var result = await DailyRunCosmeticsSettlement.ResolveFragmentGrantAsync(
            db,
            "user-1",
            "tx-empty",
            CancellationToken.None);

        Assert.NotNull(result.Error);
        Assert.Equal(string.Empty, result.FragmentName);
        Assert.Equal(0, result.Copies);
    }

    [Fact]
    public async Task ResolveFragmentGrantAsync_WhenCopiesAreNonPositive_DefaultsToOne()
    {
        await using var db = TestDbContextFactory.Create();
        await SeedChestClaimAsync(db, "user-1", "tx-default-copies", "Comet Frame Fragment", 0);
        await SeedSeasonSettlementAsync(db, "user-1", "tx-default-copies");

        var result = await DailyRunCosmeticsSettlement.ResolveFragmentGrantAsync(
            db,
            "user-1",
            "tx-default-copies",
            CancellationToken.None);

        Assert.Null(result.Error);
        Assert.Equal("Comet Frame Fragment", result.FragmentName);
        Assert.Equal(1, result.Copies);
    }

    [Fact]
    public async Task ResolveFragmentGrantAsync_WhenSettlementIsValid_ReturnsStoredReward()
    {
        await using var db = TestDbContextFactory.Create();
        await SeedChestClaimAsync(db, "user-1", "tx-valid", "Comet Frame Fragment", 3);
        await SeedSeasonSettlementAsync(db, "user-1", "tx-valid");

        var result = await DailyRunCosmeticsSettlement.ResolveFragmentGrantAsync(
            db,
            "user-1",
            "tx-valid",
            CancellationToken.None);

        Assert.Null(result.Error);
        Assert.Equal("Comet Frame Fragment", result.FragmentName);
        Assert.Equal(3, result.Copies);
    }

    [Fact]
    public async Task BuildFragmentGrantHintAsync_WhenClaimMissing_ReturnsNull()
    {
        await using var db = TestDbContextFactory.Create();

        var result = await DailyRunCosmeticsSettlement.BuildFragmentGrantHintAsync(
            db,
            "user-1",
            "missing-tx",
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task BuildFragmentGrantHintAsync_WhenFragmentMissing_ReturnsNull()
    {
        await using var db = TestDbContextFactory.Create();
        await SeedChestClaimAsync(db, "user-1", "tx-no-fragment", string.Empty, 2);

        var result = await DailyRunCosmeticsSettlement.BuildFragmentGrantHintAsync(
            db,
            "user-1",
            "tx-no-fragment",
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task BuildFragmentGrantHintAsync_DefaultsCopiesAndReturnsTransactionData()
    {
        await using var db = TestDbContextFactory.Create();
        await SeedChestClaimAsync(db, "user-1", "tx-hint", "Comet Frame Fragment", -5);

        var result = await DailyRunCosmeticsSettlement.BuildFragmentGrantHintAsync(
            db,
            "user-1",
            "tx-hint",
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("tx-hint", result!.TransactionId);
        Assert.Equal("Comet Frame Fragment", result.FragmentName);
        Assert.Equal(1, result.Copies);
    }

    private static async Task SeedChestClaimAsync(
        MathLearning.Infrastructure.Persistance.ApiDbContext db,
        string userId,
        string transactionId,
        string fragmentName,
        int copies)
    {
        db.DailyRunChestClaims.Add(new DailyRunChestClaim
        {
            UserId = userId,
            Day = new DateOnly(2026, 7, 2),
            TransactionId = transactionId,
            Xp = 30,
            Coins = 10,
            CosmeticFragment = fragmentName,
            FragmentCopies = copies
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedSeasonSettlementAsync(
        MathLearning.Infrastructure.Persistance.ApiDbContext db,
        string userId,
        string transactionId)
    {
        db.UserSeasonDailyRunClaims.Add(new UserSeasonDailyRunClaim
        {
            UserId = userId,
            SeasonId = 1,
            DailyRunTransactionId = transactionId,
            AwardedXp = 30
        });
        await db.SaveChangesAsync();
    }
}
