using MathLearning.Domain.Entities;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Tests.Endpoints;

public class UserStatsCalculationTests
{
    [Fact]
    public async Task Accuracy_AllCorrect_Returns100()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        db.UserQuestionStats.Add(new UserQuestionStat
        {
            UserId = "1", QuestionId = 1, Attempts = 10, CorrectAttempts = 10
        });
        await db.SaveChangesAsync();

        var stats = await db.UserQuestionStats
            .Where(s => s.UserId == "1")
            .ToListAsync();

        var totalAttempts = stats.Sum(s => s.Attempts);
        var totalCorrect = stats.Sum(s => s.CorrectAttempts);
        var accuracy = totalAttempts > 0
            ? Math.Round((double)totalCorrect / totalAttempts * 100, 2)
            : 0;

        Assert.Equal(100.0, accuracy);
    }

    [Fact]
    public async Task Accuracy_HalfCorrect_Returns50()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        db.UserQuestionStats.Add(new UserQuestionStat
        {
            UserId = "1", QuestionId = 1, Attempts = 10, CorrectAttempts = 5
        });
        await db.SaveChangesAsync();

        var stats = await db.UserQuestionStats
            .Where(s => s.UserId == "1")
            .ToListAsync();

        var totalAttempts = stats.Sum(s => s.Attempts);
        var totalCorrect = stats.Sum(s => s.CorrectAttempts);
        var accuracy = totalAttempts > 0
            ? Math.Round((double)totalCorrect / totalAttempts * 100, 2)
            : 0;

        Assert.Equal(50.0, accuracy);
    }

    [Fact]
    public async Task Accuracy_NoAttempts_Returns0()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        var stats = await db.UserQuestionStats
            .Where(s => s.UserId == "1")
            .ToListAsync();

        var totalAttempts = stats.Sum(s => s.Attempts);
        var accuracy = totalAttempts > 0
            ? Math.Round((double)stats.Sum(s => s.CorrectAttempts) / totalAttempts * 100, 2)
            : 0;

        Assert.Equal(0, accuracy);
    }

    [Fact]
    public async Task HintUsage_CountsPerUser()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        db.UserHints.Add(new UserHint
        {
            UserId = "1", QuestionId = 1, HintType = "formula", UsedAt = DateTime.UtcNow
        });
        db.UserHints.Add(new UserHint
        {
            UserId = "1", QuestionId = 2, HintType = "clue", UsedAt = DateTime.UtcNow
        });
        db.UserHints.Add(new UserHint
        {
            UserId = "1", QuestionId = 3, HintType = "solution", UsedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var hintsUsed = await db.UserHints
            .Where(h => h.UserId == "1")
            .CountAsync();

        Assert.Equal(3, hintsUsed);
    }

    [Fact]
    public async Task CoinTracking_EarnedAndSpent_TrackedSeparately()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        var profile = await db.UserProfiles.FirstAsync(p => p.UserId == "1");

        // Earn coins
        profile.Coins += 50;
        profile.TotalCoinsEarned += 50;

        // Spend coins
        profile.Coins -= 30;
        profile.TotalCoinsSpent += 30;

        await db.SaveChangesAsync();

        var updated = await db.UserProfiles.FirstAsync(p => p.UserId == "1");
        Assert.Equal(120, updated.Coins);       // 100 + 50 - 30
        Assert.Equal(50, updated.TotalCoinsEarned);
        Assert.Equal(30, updated.TotalCoinsSpent);
    }
}
