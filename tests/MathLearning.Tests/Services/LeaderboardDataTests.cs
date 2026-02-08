using MathLearning.Domain.Entities;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Tests.Services;

public class LeaderboardDataTests
{
    [Fact]
    public async Task Leaderboard_XpCalculation_10PerCorrectAnswer()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        db.UserQuestionStats.Add(new UserQuestionStat
        {
            UserId = 1, QuestionId = 1, Attempts = 5, CorrectAttempts = 3
        });
        db.UserQuestionStats.Add(new UserQuestionStat
        {
            UserId = 1, QuestionId = 2, Attempts = 3, CorrectAttempts = 2
        });
        await db.SaveChangesAsync();

        var totalCorrect = await db.UserQuestionStats
            .Where(s => s.UserId == 1)
            .SumAsync(s => s.CorrectAttempts);

        int xp = totalCorrect * 10;

        Assert.Equal(50, xp); // 5 correct * 10 = 50 XP
    }

    [Fact]
    public async Task Leaderboard_LevelCalculation_1Per100Xp()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        db.UserQuestionStats.Add(new UserQuestionStat
        {
            UserId = 1, QuestionId = 1, Attempts = 15, CorrectAttempts = 15
        });
        await db.SaveChangesAsync();

        var totalCorrect = await db.UserQuestionStats
            .Where(s => s.UserId == 1)
            .SumAsync(s => s.CorrectAttempts);

        int xp = totalCorrect * 10; // 150
        int level = 1 + (xp / 100); // 1 + 1 = 2

        Assert.Equal(150, xp);
        Assert.Equal(2, level);
    }

    [Fact]
    public async Task Leaderboard_Ranking_OrderedByXpDescending()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        // Add second user
        db.UserProfiles.Add(new UserProfile
        {
            Id = 2, UserId = 2, Username = "user2", DisplayName = "User Two",
            Coins = 100, Level = 1, Xp = 0, Streak = 0,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });

        // User 1: 3 correct
        db.UserQuestionStats.Add(new UserQuestionStat
        {
            UserId = 1, QuestionId = 1, Attempts = 3, CorrectAttempts = 3
        });
        // User 2: 10 correct
        db.UserQuestionStats.Add(new UserQuestionStat
        {
            UserId = 2, QuestionId = 1, Attempts = 10, CorrectAttempts = 10
        });
        await db.SaveChangesAsync();

        var ranked = await (
            from s in db.UserQuestionStats
            group s by s.UserId into g
            select new
            {
                UserId = g.Key,
                TotalCorrect = g.Sum(x => x.CorrectAttempts)
            }
        )
        .OrderByDescending(x => x.TotalCorrect)
        .ToListAsync();

        Assert.Equal(2, ranked[0].UserId); // User 2 first (10 correct)
        Assert.Equal(1, ranked[1].UserId); // User 1 second (3 correct)
    }

    [Fact]
    public async Task Leaderboard_WeeklyXp_OnlyCountsThisWeek()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        var session = new QuizSession
        {
            Id = Guid.NewGuid(), UserId = 1, StartedAt = DateTime.UtcNow
        };
        db.QuizSessions.Add(session);

        // Answer this week
        db.UserAnswers.Add(new UserAnswer
        {
            UserId = 1, QuestionId = 1, QuizSessionId = session.Id,
            Answer = "2", IsCorrect = true, TimeSpentSeconds = 5,
            AnsweredAt = DateTime.UtcNow
        });

        // Answer last month
        db.UserAnswers.Add(new UserAnswer
        {
            UserId = 1, QuestionId = 2, QuizSessionId = session.Id,
            Answer = "4", IsCorrect = true, TimeSpentSeconds = 5,
            AnsweredAt = DateTime.UtcNow.AddDays(-30)
        });
        await db.SaveChangesAsync();

        DateTime weekStart = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.Date.DayOfWeek + 1);

        var weeklyCorrect = await db.UserAnswers
            .Where(a => a.AnsweredAt >= weekStart && a.UserId == 1 && a.IsCorrect)
            .CountAsync();

        Assert.Equal(1, weeklyCorrect);
    }
}
