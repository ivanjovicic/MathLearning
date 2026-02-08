using MathLearning.Domain.Entities;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Tests.Services;

public class FriendSystemTests
{
    [Fact]
    public async Task Friends_CanBeAdded()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        db.UserProfiles.Add(new UserProfile
        {
            Id = 2, UserId = 2, Username = "friend1", DisplayName = "Friend One",
            Coins = 100, Level = 1, Xp = 0, Streak = 0,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });

        db.UserFriends.Add(new UserFriend { UserId = 1, FriendId = 2 });
        await db.SaveChangesAsync();

        var friends = await db.UserFriends
            .Where(f => f.UserId == 1)
            .Select(f => f.FriendId)
            .ToListAsync();

        Assert.Single(friends);
        Assert.Contains(2, friends);
    }

    [Fact]
    public async Task Friends_LeaderboardOnlyShowsFriends()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        // Add 2 more users
        db.UserProfiles.Add(new UserProfile
        {
            Id = 2, UserId = 2, Username = "friend1", DisplayName = "Friend One",
            Coins = 100, Level = 1, Xp = 0, Streak = 0,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        db.UserProfiles.Add(new UserProfile
        {
            Id = 3, UserId = 3, Username = "stranger", DisplayName = "Stranger",
            Coins = 100, Level = 1, Xp = 0, Streak = 0,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });

        // User 1 is friends with user 2, but NOT user 3
        db.UserFriends.Add(new UserFriend { UserId = 1, FriendId = 2 });

        // All three have stats
        db.UserQuestionStats.Add(new UserQuestionStat { UserId = 1, QuestionId = 1, Attempts = 5, CorrectAttempts = 5 });
        db.UserQuestionStats.Add(new UserQuestionStat { UserId = 2, QuestionId = 1, Attempts = 3, CorrectAttempts = 3 });
        db.UserQuestionStats.Add(new UserQuestionStat { UserId = 3, QuestionId = 1, Attempts = 10, CorrectAttempts = 10 });
        await db.SaveChangesAsync();

        // Get friend IDs (+ self)
        var friendIds = await db.UserFriends
            .Where(f => f.UserId == 1)
            .Select(f => f.FriendId)
            .ToListAsync();
        friendIds.Add(1); // Include self

        var friendStats = await db.UserQuestionStats
            .Where(s => friendIds.Contains(s.UserId))
            .GroupBy(s => s.UserId)
            .Select(g => new { UserId = g.Key, TotalCorrect = g.Sum(x => x.CorrectAttempts) })
            .ToListAsync();

        // Should only include user 1 and 2, NOT user 3
        Assert.Equal(2, friendStats.Count);
        Assert.DoesNotContain(friendStats, s => s.UserId == 3);
    }

    [Fact]
    public async Task UserSearch_FindsByUsername()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        db.UserProfiles.Add(new UserProfile
        {
            Id = 2, UserId = 2, Username = "marko123", DisplayName = "Marko",
            Coins = 100, Level = 1, Xp = 0, Streak = 0,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var results = await db.UserProfiles
            .Where(p => p.Username.Contains("marko") ||
                       (p.DisplayName != null && p.DisplayName.Contains("marko")))
            .ToListAsync();

        Assert.Single(results);
        Assert.Equal("marko123", results[0].Username);
    }

    [Fact]
    public async Task UserSearch_FindsByDisplayName()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        db.UserProfiles.Add(new UserProfile
        {
            Id = 2, UserId = 2, Username = "user2", DisplayName = "Jelena Petrovic",
            Coins = 100, Level = 1, Xp = 0, Streak = 0,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var results = await db.UserProfiles
            .Where(p => p.Username.Contains("Jelena") ||
                       (p.DisplayName != null && p.DisplayName.Contains("Jelena")))
            .ToListAsync();

        Assert.Single(results);
        Assert.Equal("Jelena Petrovic", results[0].DisplayName);
    }
}
