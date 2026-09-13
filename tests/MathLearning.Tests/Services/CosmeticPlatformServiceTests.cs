using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Application.DTOs.Cosmetics;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services.Cosmetics;
using MathLearning.Infrastructure.Services.Performance;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
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

        var (season, item) = await SeedRewardTrackAsync(db, CosmeticTrackTypes.Free, xpRequired: 100);
        db.UserSeasonProgresses.Add(new UserSeasonProgress
        {
            UserId = "1",
            SeasonId = season.Id,
            EarnedXp = 150,
            Level = 1
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
        Assert.Equal(1, db.CosmeticRewardClaims.Count(x => x.UserId == "1" && x.SourceType == CosmeticUnlockTypes.RewardTrack));
        Assert.Equal(1, db.UserCosmeticInventories.Count(x => x.UserId == "1" && x.CosmeticItemId == item.Id && !x.IsRevoked));
    }

    [Fact]
    public async Task RewardTrack_UsesSeasonXp_NotLifetimeXp()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var profile = db.UserProfiles.Single(x => x.UserId == "1");
        profile.Xp = 50_000;

        var (season, _) = await SeedRewardTrackAsync(db, CosmeticTrackTypes.Free, xpRequired: 100);
        db.UserSeasonProgresses.Add(new UserSeasonProgress
        {
            UserId = "1",
            SeasonId = season.Id,
            EarnedXp = 0,
            Level = 1
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var track = await service.GetRewardTrackAsync("1", season.Id, CosmeticTrackTypes.Free, CancellationToken.None);
        Assert.NotNull(track);
        Assert.Equal(0, track!.CurrentXp);
        Assert.False(track.Tiers.Single().IsUnlocked);
        Assert.False(track.Tiers.Single().CanClaim);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ClaimRewardTrackTierAsync(
                "1",
                new ClaimRewardTrackTierRequest(season.Id, CosmeticTrackTypes.Free, 1),
                CancellationToken.None));
        Assert.Contains("not unlocked", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, db.CosmeticRewardClaims.Count(x => x.UserId == "1"));
        Assert.Equal(0, db.UserCosmeticInventories.Count(x => x.UserId == "1" && x.Source == CosmeticUnlockTypes.RewardTrack));
    }

    [Fact]
    public async Task RewardTrack_ExplicitInactiveOrFutureSeason_IsDenied()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var profile = db.UserProfiles.Single(x => x.UserId == "1");
        profile.Xp = 500;

        var draft = await SeedSeasonAsync(db, "draft-season", CosmeticSeasonStatuses.Draft, isActive: false,
            start: DateTime.UtcNow.AddDays(3), end: DateTime.UtcNow.AddDays(10));
        var future = await SeedSeasonAsync(db, "future-season", CosmeticSeasonStatuses.Scheduled, isActive: false,
            start: DateTime.UtcNow.AddDays(3), end: DateTime.UtcNow.AddDays(10));
        var archived = await SeedSeasonAsync(db, "archived-season", CosmeticSeasonStatuses.Archived, isActive: false,
            start: DateTime.UtcNow.AddDays(-30), end: DateTime.UtcNow.AddDays(-10));

        foreach (var season in new[] { draft, future, archived })
        {
            await SeedTrackEntryAsync(db, season.Id, CosmeticTrackTypes.Free, 100);
            db.UserSeasonProgresses.Add(new UserSeasonProgress
            {
                UserId = "1",
                SeasonId = season.Id,
                EarnedXp = 500,
                Level = 2
            });
        }
        await db.SaveChangesAsync();

        var service = CreateService(db);
        foreach (var season in new[] { draft, future, archived })
        {
            Assert.Null(await service.GetRewardTrackAsync("1", season.Id, CosmeticTrackTypes.Free, CancellationToken.None));
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ClaimRewardTrackTierAsync(
                    "1",
                    new ClaimRewardTrackTierRequest(season.Id, CosmeticTrackTypes.Free, 1),
                    CancellationToken.None));
            Assert.Contains("not available", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(0, db.CosmeticRewardClaims.Count());
    }

    [Fact]
    public async Task RewardTrack_PremiumWithoutEntitlement_FailsClosed()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var (season, _) = await SeedRewardTrackAsync(db, CosmeticTrackTypes.Premium, xpRequired: 50);
        db.UserSeasonProgresses.Add(new UserSeasonProgress
        {
            UserId = "1",
            SeasonId = season.Id,
            EarnedXp = 200,
            Level = 2
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        Assert.Null(await service.GetRewardTrackAsync("1", season.Id, CosmeticTrackTypes.Premium, CancellationToken.None));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ClaimRewardTrackTierAsync(
                "1",
                new ClaimRewardTrackTierRequest(season.Id, CosmeticTrackTypes.Premium, 1),
                CancellationToken.None));
        Assert.Contains("premium", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, db.CosmeticRewardClaims.Count());
        Assert.Equal(0, db.UserCosmeticInventories.Count(x => x.Source == CosmeticUnlockTypes.RewardTrack));
    }

    [Fact]
    public async Task RewardTrack_PreviewAndClaim_ShareSeasonXpAuthority()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var profile = db.UserProfiles.Single(x => x.UserId == "1");
        profile.Xp = 10;

        var (season, _) = await SeedRewardTrackAsync(db, CosmeticTrackTypes.Free, xpRequired: 100);
        db.UserSeasonProgresses.Add(new UserSeasonProgress
        {
            UserId = "1",
            SeasonId = season.Id,
            EarnedXp = 150,
            Level = 1
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var track = await service.GetRewardTrackAsync("1", season.Id, CosmeticTrackTypes.Free, CancellationToken.None);
        Assert.NotNull(track);
        Assert.Equal(150, track!.CurrentXp);
        Assert.True(track.Tiers.Single().IsUnlocked);
        Assert.True(track.Tiers.Single().CanClaim);

        var claim = await service.ClaimRewardTrackTierAsync(
            "1",
            new ClaimRewardTrackTierRequest(season.Id, CosmeticTrackTypes.Free, 1),
            CancellationToken.None);
        Assert.True(claim.Success);
        Assert.False(claim.AlreadyClaimed);
        Assert.Single(claim.Rewards);
    }

    private static async Task<(CosmeticSeason Season, CosmeticItem Item)> SeedRewardTrackAsync(
        ApiDbContext db,
        string trackType,
        int xpRequired)
    {
        await EnsureCatalogReadyAsync(db);
        var season = await SeedSeasonAsync(
            db,
            $"reward-track-{Guid.NewGuid():N}",
            CosmeticSeasonStatuses.Active,
            isActive: true,
            start: DateTime.UtcNow.AddDays(-7),
            end: DateTime.UtcNow.AddDays(7));
        var item = await SeedTrackEntryAsync(db, season.Id, trackType, xpRequired);
        return (season, item);
    }

    private static async Task EnsureCatalogReadyAsync(ApiDbContext db)
    {
        if (await db.CosmeticCatalogRevisions.AnyAsync())
        {
            return;
        }

        var manifest = CosmeticCatalogManifestProvider.Current;
        db.CosmeticCatalogRevisions.Add(new CosmeticCatalogRevision
        {
            RevisionKey = manifest.RevisionKey,
            Checksum = manifest.Checksum,
            AppliedBy = "test",
            AppliedAtUtc = DateTime.UtcNow
        });

        foreach (var key in manifest.RequiredDefaultKeys)
        {
            if (await db.CosmeticItems.AnyAsync(x => x.Key == key))
            {
                continue;
            }

            db.CosmeticItems.Add(new CosmeticItem
            {
                Key = key,
                Name = key,
                Category = CosmeticCategories.Skin,
                Rarity = "common",
                AssetPath = $"cosmetics/{key}.png",
                UnlockType = CosmeticUnlockTypes.Default,
                IsDefault = true,
                IsActive = true,
                ReleaseDate = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow
            });
        }

        foreach (var fragment in manifest.RequiredFragments)
        {
            if (await db.CosmeticItems.AnyAsync(x => x.Key == fragment.Key))
            {
                continue;
            }

            db.CosmeticItems.Add(new CosmeticItem
            {
                Key = fragment.Key,
                Name = fragment.Key,
                Category = CosmeticCategories.Frame,
                Rarity = "epic",
                AssetPath = $"cosmetics/{fragment.Key}.png",
                UnlockType = "fragment",
                FragmentLabel = fragment.FragmentLabel,
                FragmentsRequired = fragment.FragmentsRequired,
                IsActive = true,
                ReleaseDate = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task<CosmeticSeason> SeedSeasonAsync(
        ApiDbContext db,
        string key,
        string status,
        bool isActive,
        DateTime start,
        DateTime end)
    {
        var season = new CosmeticSeason
        {
            Key = key,
            Name = key,
            Status = status,
            StartDate = start,
            EndDate = end,
            IsActive = isActive
        };
        db.CosmeticSeasons.Add(season);
        await db.SaveChangesAsync();
        return season;
    }

    private static async Task<CosmeticItem> SeedTrackEntryAsync(
        ApiDbContext db,
        int seasonId,
        string trackType,
        int xpRequired)
    {
        var item = new CosmeticItem
        {
            Key = $"track-tier-{Guid.NewGuid():N}",
            Name = "Tier 1 Reward",
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
            SeasonId = seasonId,
            TrackType = trackType,
            Tier = 1,
            XpRequired = xpRequired,
            RewardType = "cosmetic_item",
            RewardPayloadJson = JsonSerializer.Serialize(new { cosmeticItemId = item.Id }),
            IsActive = true
        });
        await db.SaveChangesAsync();
        return item;
    }

    private static CosmeticPlatformService CreateService(ApiDbContext db)
        => new(
            db,
            NullLogger<CosmeticPlatformService>.Instance,
            new HybridCacheService(
                new MemoryCache(new MemoryCacheOptions { SizeLimit = 1000 }),
                NullLogger<HybridCacheService>.Instance),
            new AvatarAppearanceReader(db));
}

public sealed class RewardTrackAuthorityEndpointTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> factory;
    private readonly HttpClient client;

    public RewardTrackAuthorityEndpointTests(CustomWebApplicationFactory<Program> factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task HttpRewardTrack_LifetimeXpWithoutSeasonXp_CannotClaim()
    {
        var userId = $"rt-lifetime-{Guid.NewGuid():N}";
        var seasonId = 0;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            if (await userManager.FindByIdAsync(userId) is null)
            {
                await userManager.CreateAsync(new IdentityUser { Id = userId, UserName = userId });
            }

            db.UserProfiles.Add(new UserProfile
            {
                UserId = userId,
                Xp = 50_000,
                Level = 40,
                Coins = 0,
                UpdatedAt = DateTime.UtcNow
            });

            var season = new CosmeticSeason
            {
                Key = $"http-rt-{Guid.NewGuid():N}",
                Name = "HTTP Reward Track",
                Status = CosmeticSeasonStatuses.Active,
                StartDate = DateTime.UtcNow.AddDays(-3),
                EndDate = DateTime.UtcNow.AddDays(3),
                IsActive = true
            };
            db.CosmeticSeasons.Add(season);
            await db.SaveChangesAsync();
            seasonId = season.Id;

            var item = new CosmeticItem
            {
                Key = $"http-rt-item-{Guid.NewGuid():N}",
                Name = "HTTP Tier",
                Category = CosmeticCategories.Emoji,
                AssetPath = "cosmetics/emoji/http.png",
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
            db.UserSeasonProgresses.Add(new UserSeasonProgress
            {
                UserId = userId,
                SeasonId = season.Id,
                EarnedXp = 0,
                Level = 1
            });
            await db.SaveChangesAsync();
        }

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/cosmetics/reward-track?seasonId={seasonId}&trackType=free");
        getRequest.Headers.Add("X-Test-UserId", userId);
        var preview = await client.SendAsync(getRequest);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var previewBody = await preview.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, previewBody.GetProperty("currentXp").GetInt32());
        Assert.False(previewBody.GetProperty("tiers")[0].GetProperty("isUnlocked").GetBoolean());

        using var claimRequest = new HttpRequestMessage(HttpMethod.Post, "/api/cosmetics/reward-track/claim")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { seasonId, trackType = "free", tier = 1 }),
                Encoding.UTF8,
                "application/json")
        };
        claimRequest.Headers.Add("X-Test-UserId", userId);
        var claim = await client.SendAsync(claimRequest);
        Assert.Equal(HttpStatusCode.BadRequest, claim.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
            Assert.Equal(0, await db.CosmeticRewardClaims.CountAsync(x => x.UserId == userId));
        }
    }

    [Fact]
    public async Task HttpRewardTrack_PremiumWithoutEntitlement_IsDenied()
    {
        var userId = $"rt-premium-{Guid.NewGuid():N}";
        var seasonId = 0;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            if (await userManager.FindByIdAsync(userId) is null)
            {
                await userManager.CreateAsync(new IdentityUser { Id = userId, UserName = userId });
            }

            db.UserProfiles.Add(new UserProfile
            {
                UserId = userId,
                Xp = 200,
                Level = 3,
                UpdatedAt = DateTime.UtcNow
            });

            var season = new CosmeticSeason
            {
                Key = $"http-prem-{Guid.NewGuid():N}",
                Name = "Premium Season",
                Status = CosmeticSeasonStatuses.Active,
                StartDate = DateTime.UtcNow.AddDays(-3),
                EndDate = DateTime.UtcNow.AddDays(3),
                IsActive = true
            };
            db.CosmeticSeasons.Add(season);
            await db.SaveChangesAsync();
            seasonId = season.Id;

            var item = new CosmeticItem
            {
                Key = $"http-prem-item-{Guid.NewGuid():N}",
                Name = "Premium Tier",
                Category = CosmeticCategories.Emoji,
                AssetPath = "cosmetics/emoji/prem.png",
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
                TrackType = CosmeticTrackTypes.Premium,
                Tier = 1,
                XpRequired = 50,
                RewardType = "cosmetic_item",
                RewardPayloadJson = JsonSerializer.Serialize(new { cosmeticItemId = item.Id }),
                IsActive = true
            });
            db.UserSeasonProgresses.Add(new UserSeasonProgress
            {
                UserId = userId,
                SeasonId = season.Id,
                EarnedXp = 200,
                Level = 2
            });
            await db.SaveChangesAsync();
        }

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/cosmetics/reward-track?seasonId={seasonId}&trackType=premium");
        getRequest.Headers.Add("X-Test-UserId", userId);
        var preview = await client.SendAsync(getRequest);
        Assert.Equal(HttpStatusCode.NotFound, preview.StatusCode);

        using var claimRequest = new HttpRequestMessage(HttpMethod.Post, "/api/cosmetics/reward-track/claim")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { seasonId, trackType = "premium", tier = 1 }),
                Encoding.UTF8,
                "application/json")
        };
        claimRequest.Headers.Add("X-Test-UserId", userId);
        var claim = await client.SendAsync(claimRequest);
        Assert.Equal(HttpStatusCode.BadRequest, claim.StatusCode);
    }
}
