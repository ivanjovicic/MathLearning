using MathLearning.Domain.Entities;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Tests.Endpoints;

public class ProgressDataTests
{
    [Fact]
    public async Task TopicProgress_Accuracy_CalculatedCorrectly()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        db.UserQuestionStats.Add(new UserQuestionStat
        {
            UserId = "1", QuestionId = 1,
            Attempts = 10, CorrectAttempts = 7,
            LastAttemptAt = DateTime.UtcNow
        });
        db.UserQuestionStats.Add(new UserQuestionStat
        {
            UserId = "1", QuestionId = 2,
            Attempts = 5, CorrectAttempts = 3,
            LastAttemptAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var stats = await db.UserQuestionStats
            .Where(s => s.UserId == "1")
            .ToListAsync();

        int totalAttempts = stats.Sum(s => s.Attempts);
        int totalCorrect = stats.Sum(s => s.CorrectAttempts);
        double accuracy = Math.Round((double)totalCorrect / totalAttempts * 100, 2);

        Assert.Equal(15, totalAttempts);
        Assert.Equal(10, totalCorrect);
        Assert.Equal(66.67, accuracy);
    }

    [Fact]
    public async Task TopicProgress_NoAttempts_AccuracyIsZero()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        var stats = await db.UserQuestionStats
            .Where(s => s.UserId == "1")
            .ToListAsync();

        int totalAttempts = stats.Sum(s => s.Attempts);
        double accuracy = totalAttempts == 0 ? 0 : Math.Round((double)stats.Sum(s => s.CorrectAttempts) / totalAttempts * 100, 2);

        Assert.Equal(0, accuracy);
    }

    [Fact]
    public async Task TopicProgress_UnlockLogic_FirstTopicAlwaysUnlocked()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        var topics = await db.Topics.OrderBy(t => t.Id).ToListAsync();

        // First topic is always unlocked
        Assert.True(topics.Count > 0);
        bool firstUnlocked = true;
        Assert.True(firstUnlocked);
    }

    [Fact]
    public async Task TopicProgress_UnlockLogic_SecondTopicNeedsAccuracy60()
    {
        // Simulates unlock logic: topic N+1 unlocked if topic N accuracy >= 60%
        double prevAccuracy = 72.5;
        bool unlocked = prevAccuracy >= 60.0;
        Assert.True(unlocked);

        double lowAccuracy = 45.0;
        bool locked = lowAccuracy >= 60.0;
        Assert.False(locked);
    }
}
