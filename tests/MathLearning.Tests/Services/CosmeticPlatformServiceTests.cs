using System.Text.Json;
using MathLearning.Application.DTOs.Cosmetics;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Services.Cosmetics;
using MathLearning.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace MathLearning.Tests.Services;

public class CosmeticPlatformServiceTests
{
    [Fact]
    public async Task ProcessRewardSourceAsync_LeaderboardUnlock_GrantsLegacyCosmetic()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        db.CosmeticItems.Add(new CosmeticItem
        {
            Key = "top-10-frame",
            Name = "Top 10 Frame",
            Category = CosmeticCategories.Frame,
            Rarity = "epic",
            AssetPath = "cosmetics/frame/top10.png",
            UnlockType = CosmeticUnlockTypes.Leaderboard,
            UnlockCondition = "top:10",
            IsActive = true,
            ReleaseDate = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var rewards = await service.ProcessRewardSourceAsync(
            new CosmeticRewardSourceRequest(
                "1",
                CosmeticUnlockTypes.Leaderboard,
                "leaderboard:global:week:20260302",
                JsonSerializer.Serialize(new { scope = "global", period = "week", rank = 5, percentile = 10 })),
            CancellationToken.None);

        Assert.Single(rewards);
        Assert.Equal(CosmeticUnlockTypes.Leaderboard, rewards[0].SourceType);
        Assert.True(db.UserCosmeticInventories.Any(x => x.UserId == "1" && x.CosmeticItemId == rewards[0].CosmeticItemId));
    }

    [Fact]
    public async Task ProcessRewardSourceAsync_SchoolCompetitionUnlock_GrantsLegacyCosmetic()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        db.CosmeticItems.Add(new CosmeticItem
        {
            Key = "school-podium-bg",
            Name = "School Podium Background",
            Category = CosmeticCategories.Background,
            Rarity = "legendary",
            AssetPath = "cosmetics/background/school_podium.png",
            UnlockType = CosmeticUnlockTypes.SchoolCompetition,
            UnlockCondition = "top:3",
            IsActive = true,
            ReleaseDate = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var rewards = await service.ProcessRewardSourceAsync(
            new CosmeticRewardSourceRequest(
                "1",
                CosmeticUnlockTypes.SchoolCompetition,
                "school-competition:week:20260302",
                JsonSerializer.Serialize(new { period = "week", schoolId = 15, placement = 2, rank = 2 })),
            CancellationToken.None);

        Assert.Single(rewards);
        Assert.Equal(CosmeticUnlockTypes.SchoolCompetition, rewards[0].SourceType);
    }

    [Fact]
    public async Task ClaimRewardTrackTierAsync_ClaimsOnce_AndReturnsAlreadyClaimedOnReplay()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var profile = db.UserProfiles.Single(x => x.UserId == "1");
        profile.Xp = 500;
        profile.Level = 6;

        var season = new CosmeticSeason
        {
            Key = "math-olympiad",
            Name = "Math Olympiad",
            Status = CosmeticSeasonStatuses.Active,
            StartDate = DateTime.UtcNow.AddDays(-7),
            EndDate = DateTime.UtcNow.AddDays(7),
            IsActive = true
        };
        db.CosmeticSeasons.Add(season);

        var item = new CosmeticItem
        {
            Key = "track-tier-1-emoji",
            Name = "Tier 1 Emoji",
            Category = CosmeticCategories.Emoji,
            Rarity = "common",
            AssetPath = "cosmetics/emoji/tier1.png",
            UnlockType = CosmeticUnlockTypes.RewardTrack,
            IsActive = true,
            ReleaseDate = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow
        };
        db.CosmeticItems.Add(item);
        await db.SaveChangesAsync();

        db.SeasonRewardTrackEntries.Add(new SeasonRewardTrackEntry
        {
            SeasonId = season.Id,
            TrackType = CosmeticTrackTypes.Free,
            Tier = 1,
            XpRequired = 100,
            RewardType = "cosmetic_item",
            RewardPayloadJson = JsonSerializer.Serialize(new { cosmeticItemId = item.Id }),
            IsActive = true
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var claim = await service.ClaimRewardTrackTierAsync(
            "1",
            new ClaimRewardTrackTierRequest(season.Id, CosmeticTrackTypes.Free, 1),
            CancellationToken.None);
        var replay = await service.ClaimRewardTrackTierAsync(
            "1",
            new ClaimRewardTrackTierRequest(season.Id, CosmeticTrackTypes.Free, 1),
            CancellationToken.None);
        var track = await service.GetRewardTrackAsync("1", season.Id, CosmeticTrackTypes.Free, CancellationToken.None);

        Assert.True(claim.Success);
        Assert.False(claim.AlreadyClaimed);
        Assert.Single(claim.Rewards);
        Assert.True(replay.AlreadyClaimed);
        Assert.NotNull(track);
        Assert.True(track!.Tiers.Single().IsClaimed);
        Assert.False(track.Tiers.Single().CanClaim);
    }

    private static CosmeticPlatformService CreateService(MathLearning.Infrastructure.Persistance.ApiDbContext db)
        => new(db, NullLogger<CosmeticPlatformService>.Instance);
}
