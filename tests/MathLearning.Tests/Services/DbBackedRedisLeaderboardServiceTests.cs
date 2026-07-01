using MathLearning.Core.DTOs;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Services.Leaderboard;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;

namespace MathLearning.Tests.Services;

public sealed class DbBackedRedisLeaderboardServiceTests
{
    [Fact]
    public async Task GetLeaderboardAsync_UsesSqlFallbackAndPeriodScoreOrdering()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync();
        db.Users.AddRange(
            new IdentityUser { Id = "2", UserName = "u2", Email = "u2@test.local" },
            new IdentityUser { Id = "3", UserName = "u3", Email = "u3@test.local" });
        db.UserProfiles.AddRange(
            new UserProfile
            {
                UserId = "2",
                Username = "u2",
                DisplayName = "User Two",
                Level = 4,
                Xp = 1400,
                WeeklyXp = 250,
                Streak = 5
            },
            new UserProfile
            {
                UserId = "3",
                Username = "u3",
                DisplayName = "User Three",
                Level = 6,
                Xp = 1600,
                WeeklyXp = 500,
                Streak = 9
            });
        await db.SaveChangesAsync();

        var sut = new DbBackedRedisLeaderboardService(db, NullLogger<DbBackedRedisLeaderboardService>.Instance);

        var leaderboard = await sut.GetLeaderboardAsync(new LeaderboardRequestDto
        {
            Scope = "global",
            Period = "weekly",
            Limit = 10,
            UserId = "1"
        });

        Assert.Equal(3, leaderboard.Count);
        Assert.Equal("3", leaderboard[0].UserId);
        Assert.Equal(500, leaderboard[0].Xp);
        Assert.Equal("2", leaderboard[1].UserId);
        Assert.Equal("1", leaderboard[2].UserId);
    }

    [Fact]
    public async Task GetUserRankAsync_UsesSqlFallbackForFriendsScope()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync();
        db.Users.AddRange(
            new IdentityUser { Id = "2", UserName = "u2", Email = "u2@test.local" },
            new IdentityUser { Id = "3", UserName = "u3", Email = "u3@test.local" });
        db.UserProfiles.AddRange(
            new UserProfile
            {
                UserId = "2",
                Username = "u2",
                DisplayName = "User Two",
                Level = 4,
                Xp = 1200,
                WeeklyXp = 250,
                Streak = 5
            },
            new UserProfile
            {
                UserId = "3",
                Username = "u3",
                DisplayName = "User Three",
                Level = 6,
                Xp = 1800,
                WeeklyXp = 500,
                Streak = 9
            });
        db.UserFriends.Add(new UserFriend { UserId = "1", FriendId = "2" });
        await db.SaveChangesAsync();

        var sut = new DbBackedRedisLeaderboardService(db, NullLogger<DbBackedRedisLeaderboardService>.Instance);

        var me = await sut.GetUserRankAsync(new LeaderboardRequestDto
        {
            Scope = "friends",
            Period = "weekly",
            UserId = "1"
        });

        Assert.NotNull(me);
        Assert.Equal(2, me!.Rank);
        Assert.Equal("1", me.UserId);
    }

    [Fact]
    public async Task GetUserRankAsync_TiebreakerUsesLexicographicUserId()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync();
        db.Users.AddRange(
            new IdentityUser { Id = "2", UserName = "u2", Email = "u2@test.local" },
            new IdentityUser { Id = "10", UserName = "u10", Email = "u10@test.local" });
        db.UserProfiles.AddRange(
            new UserProfile
            {
                UserId = "2",
                Username = "u2",
                DisplayName = "User Two",
                Level = 1,
                Xp = 100,
                WeeklyXp = 100,
                Streak = 1
            },
            new UserProfile
            {
                UserId = "10",
                Username = "u10",
                DisplayName = "User Ten",
                Level = 1,
                Xp = 100,
                WeeklyXp = 100,
                Streak = 1
            });
        await db.SaveChangesAsync();

        var sut = new DbBackedRedisLeaderboardService(db, NullLogger<DbBackedRedisLeaderboardService>.Instance);

        var userTen = await sut.GetUserRankAsync(new LeaderboardRequestDto
        {
            Scope = "global",
            Period = "weekly",
            UserId = "10"
        });
        var userTwo = await sut.GetUserRankAsync(new LeaderboardRequestDto
        {
            Scope = "global",
            Period = "weekly",
            UserId = "2"
        });

        Assert.NotNull(userTen);
        Assert.NotNull(userTwo);
        Assert.Equal(1, userTen!.Rank);
        Assert.Equal(2, userTwo!.Rank);
    }

    [Fact]
    public async Task GetNearRivalsAsync_ReturnsFiveUserWindowAroundRank()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync();
        for (var i = 2; i <= 8; i++)
        {
            db.Users.Add(new IdentityUser { Id = i.ToString(), UserName = $"u{i}", Email = $"u{i}@test.local" });
            db.UserProfiles.Add(new UserProfile
            {
                UserId = i.ToString(),
                Username = $"u{i}",
                DisplayName = $"User {i}",
                Level = 1,
                Xp = i * 100,
                WeeklyXp = i * 100,
                Streak = 1
            });
        }

        await db.SaveChangesAsync();

        var sut = new DbBackedRedisLeaderboardService(db, NullLogger<DbBackedRedisLeaderboardService>.Instance);

        var rivals = await sut.GetNearRivalsAsync(new LeaderboardRequestDto
        {
            Scope = "global",
            Period = "weekly",
            UserId = "5"
        });

        Assert.Equal(5, rivals.Count);
        Assert.Equal(new[] { 2, 3, 4, 5, 6 }, rivals.Select(x => x.Rank).ToArray());
        Assert.Equal("5", rivals[2].UserId);
        Assert.Equal(4, rivals[2].Rank);
    }
}
