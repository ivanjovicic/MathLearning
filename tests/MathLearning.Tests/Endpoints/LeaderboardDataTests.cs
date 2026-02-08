using MathLearning.Domain.Entities;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Tests.Endpoints;

public class LeaderboardDataTests
{
    [Fact]
    public async Task Leaderboard_XpCalculation_CorrectAttemptsTimesTo10()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        var session = new QuizSession { Id = Guid.NewGuid(), UserId = 1, StartedAt = DateTime.UtcNow };
        db.QuizSessions.Add(session);

        db.UserQuestionStats.Add(new UserQuestionStat
        {
            UserId = 1,
            QuestionId = 1,
            Attempts = 5,
            CorrectAttempts = 3,
            LastAttemptAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var stats = await db.UserQuestionStats
            .Where(s => s.UserId == 1)
            .ToListAsync();

        int totalCorrect = stats.Sum(s => s.CorrectAttempts);
        int xp = totalCorrect * 10;
        int level = 1 + xp / 100;

        Assert.Equal(30, xp);
        Assert.Equal(1, level);
    }

    [Fact]
    public async Task Leaderboard_WeeklyXp_OnlyCountsThisWeek()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        var session = new QuizSession { Id = Guid.NewGuid(), UserId = 1, StartedAt = DateTime.UtcNow };
        db.QuizSessions.Add(session);

        // Answer this week
        db.UserAnswers.Add(new UserAnswer
        {
            UserId = 1, QuestionId = 1, QuizSessionId = session.Id,
            Answer = "2", IsCorrect = true, TimeSpentSeconds = 3,
            AnsweredAt = DateTime.UtcNow
        });

        // Answer from last month — should NOT count
        db.UserAnswers.Add(new UserAnswer
        {
            UserId = 1, QuestionId = 2, QuizSessionId = session.Id,
            Answer = "4", IsCorrect = true, TimeSpentSeconds = 3,
            AnsweredAt = DateTime.UtcNow.AddDays(-30)
        });
        await db.SaveChangesAsync();

        DateTime weekStart = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.Date.DayOfWeek + 1);

        var weeklyCorrect = await db.UserAnswers
            .Where(a => a.AnsweredAt >= weekStart && a.IsCorrect && a.UserId == 1)
            .CountAsync();

        Assert.Equal(1, weeklyCorrect);
    }

    [Fact]
    public async Task Leaderboard_ProfileDisplayName_UsedForRanking()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        var profile = await db.UserProfiles.FirstAsync(p => p.UserId == 1);

        Assert.Equal("Test User", profile.DisplayName);
        Assert.Equal("testuser", profile.Username);
    }

    [Fact]
    public async Task Leaderboard_FriendsFilter_OnlyShowsFriends()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        // Add users
        db.UserProfiles.Add(new UserProfile
        {
            Id = 2, UserId = 2, Username = "friend1",
            DisplayName = "Friend One", Coins = 100, Level = 1,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        db.UserProfiles.Add(new UserProfile
        {
            Id = 3, UserId = 3, Username = "stranger",
            DisplayName = "Stranger", Coins = 100, Level = 1,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });

        // User 1 is friends with User 2, but NOT User 3
        db.UserFriends.Add(new UserFriend { UserId = 1, FriendId = 2 });
        await db.SaveChangesAsync();

        var friendIds = await db.UserFriends
            .Where(f => f.UserId == 1)
            .Select(f => f.FriendId)
            .ToListAsync();
        friendIds.Add(1); // include self

        Assert.Contains(2, friendIds);
        Assert.DoesNotContain(3, friendIds);
    }
}
