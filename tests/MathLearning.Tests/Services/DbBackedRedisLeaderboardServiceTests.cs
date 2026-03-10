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
}
