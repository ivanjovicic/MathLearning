using MathLearning.Api;
using MathLearning.Api.Endpoints;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Endpoints;

public sealed class SeasonDailyRunPostgresTests
{
    [Fact]
    [Trait("Category", "PostgresProvider")]
    public async Task DailyRunClaim_Postgres_BindsChestDayToOneSeasonAndReplaysOriginalOwner()
    {
        if (!IsValidationRequired())
        {
            return;
        }

        await using var database = await PostgresTestDatabase.CreateAsync();
        await database.MigrateApiAsync();
        await using var factory = new PostgresWebApplicationFactory<Program>(database);

        var userId = $"season-daily-pg-{Guid.NewGuid():N}";
        var chestTransactionId = $"sdr-{Guid.NewGuid():N}";

        var oldSeasonId = await SeedSeasonAsync(
            factory,
            "daily-run-old",
            "Old Daily Run Season",
            new DateTime(2031, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2031, 2, 28, 23, 59, 59, DateTimeKind.Utc));
        var laterSeasonId = await SeedSeasonAsync(
            factory,
            "daily-run-later",
            "Later Daily Run Season",
            new DateTime(2031, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2031, 3, 31, 23, 59, 59, DateTimeKind.Utc));

        await EnsureUserAsync(factory, userId);
        await SeedDailyRunChestClaimAsync(factory, userId, chestTransactionId, new DateOnly(2031, 2, 10), xp: 40);
        var first = await SettleDailyRunAsync(
            factory,
            userId,
            chestTransactionId,
            oldSeasonId,
            idempotencyKey: $"sdr-old-{Guid.NewGuid():N}",
            operationId: $"sdr-op-{Guid.NewGuid():N}");

        Assert.True(first.Success);
        Assert.False(first.AlreadyClaimed);
        Assert.Equal(40, first.AwardedXp);
        Assert.Equal(oldSeasonId, first.Season.SeasonId);
        Assert.Equal(40, first.Season.EarnedXp);

        var replay = await SettleDailyRunAsync(
            factory,
            userId,
            chestTransactionId,
            laterSeasonId,
            idempotencyKey: $"sdr-replay-{Guid.NewGuid():N}",
            operationId: $"sdr-replay-op-{Guid.NewGuid():N}");

        Assert.True(replay.Success);
        Assert.True(replay.AlreadyClaimed);
        Assert.Equal(0, replay.AwardedXp);
        Assert.Equal(oldSeasonId, replay.Season.SeasonId);
        Assert.Equal(40, replay.Season.EarnedXp);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
            Assert.Equal(1, await db.UserSeasonProgresses.CountAsync(x => x.UserId == userId && x.SeasonId == oldSeasonId));
            Assert.Equal(0, await db.UserSeasonProgresses.CountAsync(x => x.UserId == userId && x.SeasonId == laterSeasonId));
            Assert.Equal(1, await db.UserSeasonDailyRunClaims.CountAsync(x => x.UserId == userId && x.SeasonId == oldSeasonId));
            Assert.Equal(0, await db.UserSeasonDailyRunClaims.CountAsync(x => x.UserId == userId && x.SeasonId == laterSeasonId));
            Assert.Equal(2, await db.EconomyTransactions.CountAsync(x => x.UserId == userId && x.TransactionType == "season_daily_run_claim"));
        }
    }

    private static async Task<SeasonDailyRunClaimResponse> SettleDailyRunAsync(
        PostgresWebApplicationFactory<Program> factory,
        string userId,
        string transactionId,
        int? seasonId,
        string idempotencyKey,
        string operationId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var txService = scope.ServiceProvider.GetRequiredService<IEconomyTransactionService>();

        var requestPayload = new
        {
            idempotencyKey,
            operationId,
            transactionId,
            seasonId,
            xp = 40
        };

        var begin = await txService.BeginOrGetExistingAsync(
            userId,
            "season_daily_run_claim",
            idempotencyKey,
            requestPayload,
            operationId: operationId);
        Assert.True(begin.ShouldProcess);

        var existing = await db.UserSeasonDailyRunClaims
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.DailyRunTransactionId == transactionId);
        if (existing is not null)
        {
            var seasonStateExisting = await BuildSeasonStateAsync(db, userId, existing.SeasonId);
            var replay = new SeasonDailyRunClaimResponse(
                Success: true,
                AlreadyClaimed: true,
                AwardedXp: 0,
                Season: seasonStateExisting,
                FragmentGrant: null,
                ErrorCode: null,
                Message: null);
            await txService.CompleteAsync(begin.TransactionId, replay);
            return replay;
        }

        var dailyRunClaim = await db.DailyRunChestClaims
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.TransactionId == transactionId);
        Assert.NotNull(dailyRunClaim);

        var ownerSeason = await ResolveSeasonOwningChestDayAsync(db, dailyRunClaim!.Day);
        Assert.NotNull(ownerSeason);
        Assert.Equal(seasonId, ownerSeason!.Id);

        var progress = await GetOrCreateSeasonProgressAsync(db, userId, ownerSeason.Id);
        progress.EarnedXp += dailyRunClaim.Xp;
        progress.Level = 1 + (progress.EarnedXp / 100);
        progress.UpdatedAtUtc = DateTime.UtcNow;

        db.UserSeasonDailyRunClaims.Add(new UserSeasonDailyRunClaim
        {
            UserId = userId,
            SeasonId = ownerSeason.Id,
            DailyRunTransactionId = transactionId,
            DailyRunClaimId = dailyRunClaim.Id,
            AwardedXp = dailyRunClaim.Xp,
            CreatedAtUtc = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var seasonState = await BuildSeasonStateAsync(db, userId, ownerSeason.Id, trackedProgress: progress);
        var response = new SeasonDailyRunClaimResponse(
            Success: true,
            AlreadyClaimed: false,
            AwardedXp: dailyRunClaim.Xp,
            Season: seasonState,
            FragmentGrant: null,
            ErrorCode: null,
            Message: null);

        await txService.CompleteAsync(begin.TransactionId, response);
        return response;
    }

    private static async Task EnsureUserAsync(PostgresWebApplicationFactory<Program> factory, string userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        if (!await db.Users.AnyAsync(x => x.Id == userId))
        {
            db.Users.Add(new IdentityUser
            {
                Id = userId,
                UserName = userId,
                Email = $"{userId}@example.test"
            });
        }

        if (!await db.UserProfiles.AnyAsync(x => x.UserId == userId))
        {
            db.UserProfiles.Add(new UserProfile
            {
                UserId = userId,
                Username = userId,
                DisplayName = userId,
                Coins = 0,
                Xp = 0,
                Level = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task<int> SeedSeasonAsync(
        PostgresWebApplicationFactory<Program> factory,
        string keySuffix,
        string name,
        DateTime startDate,
        DateTime endDate)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        var season = new CosmeticSeason
        {
            Key = keySuffix,
            Name = name,
            Status = CosmeticSeasonStatuses.Active,
            StartDate = startDate,
            EndDate = endDate,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.CosmeticSeasons.Add(season);
        await db.SaveChangesAsync();
        return season.Id;
    }

    private static async Task SeedDailyRunChestClaimAsync(
        PostgresWebApplicationFactory<Program> factory,
        string userId,
        string transactionId,
        DateOnly day,
        int xp)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        db.DailyRunChestClaims.Add(new DailyRunChestClaim
        {
            UserId = userId,
            Day = day,
            TransactionId = transactionId,
            Xp = xp,
            Coins = 0,
            CosmeticFragment = "Comet Frame Fragment",
            FragmentCopies = 1,
            CreatedAtUtc = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    private static async Task<UserSeasonProgress> GetOrCreateSeasonProgressAsync(
        ApiDbContext db,
        string userId,
        int seasonId)
    {
        var progress = await db.UserSeasonProgresses.FirstOrDefaultAsync(x => x.UserId == userId && x.SeasonId == seasonId);
        if (progress is not null)
            return progress;

        progress = new UserSeasonProgress
        {
            UserId = userId,
            SeasonId = seasonId,
            EarnedXp = 0,
            Level = 1,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.UserSeasonProgresses.Add(progress);
        return progress;
    }

    private static async Task<SeasonStateResponse> BuildSeasonStateAsync(
        ApiDbContext db,
        string userId,
        int seasonId,
        UserSeasonProgress? trackedProgress = null)
    {
        var progress = trackedProgress ?? await db.UserSeasonProgresses.FirstOrDefaultAsync(x => x.UserId == userId && x.SeasonId == seasonId);
        var earnedXp = progress?.EarnedXp ?? 0;
        var level = progress?.Level ?? 1;
        var claimedIds = await db.UserSeasonMilestoneClaims
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.SeasonId == seasonId)
            .Select(x => x.MilestoneId)
            .OrderBy(x => x)
            .ToListAsync();

        return new SeasonStateResponse(seasonId, earnedXp, level, claimedIds);
    }

    private static async Task<CosmeticSeason?> ResolveSeasonOwningChestDayAsync(ApiDbContext db, DateOnly chestDay)
    {
        var seasons = await db.CosmeticSeasons.AsNoTracking().ToListAsync();
        var matches = seasons.Where(season =>
        {
            var startDay = DateOnly.FromDateTime(AsUtc(season.StartDate));
            var endDay = DateOnly.FromDateTime(AsUtc(season.EndDate));
            return chestDay >= startDay && chestDay <= endDay;
        }).ToList();

        return matches.Count == 1 ? matches[0] : null;
    }

    private static DateTime AsUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    private static bool IsValidationRequired()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable("POSTGRES_PROVIDER_TESTS_REQUIRED"),
            "1",
            StringComparison.Ordinal);
    }
}
