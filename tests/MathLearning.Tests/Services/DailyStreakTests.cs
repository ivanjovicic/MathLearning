using MathLearning.Application.DTOs.Quiz;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Services;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Tests.Services;

public class DailyStreakTests
{
    [Fact]
    public async Task DailyStreak_LessThan5SrsUpdates_DoesNotComplete()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var srs = new SrsService(db);

        // Only 4 answers — not enough for daily completion
        for (int i = 1; i <= 4; i++)
        {
            await srs.UpdateAsync("1", new SrsUpdateDto { QuestionId = i, IsCorrect = true, TimeMs = 1000 });
        }

        var dailyStat = await db.UserDailyStats
            .FirstOrDefaultAsync(x => x.UserId == "1");

        // Entry should exist but not be completed
        Assert.NotNull(dailyStat);
        Assert.False(dailyStat.Completed);
    }

    [Fact]
    public async Task DailyStreak_5SrsUpdates_CompletesDay()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var srs = new SrsService(db);

        for (int i = 1; i <= 5; i++)
        {
            await srs.UpdateAsync("1", new SrsUpdateDto { QuestionId = i, IsCorrect = true, TimeMs = 1000 });
        }

        var dailyStat = await db.UserDailyStats
            .FirstOrDefaultAsync(x => x.UserId == "1");

        Assert.NotNull(dailyStat);
        Assert.True(dailyStat.Completed);
    }

    [Fact]
    public async Task DailyStreak_5SrsUpdates_IncreasesProfileStreak()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var srs = new SrsService(db);

        for (int i = 1; i <= 5; i++)
        {
            await srs.UpdateAsync("1", new SrsUpdateDto { QuestionId = i, IsCorrect = true, TimeMs = 1000 });
        }

        var profile = await db.UserProfiles.FirstAsync(p => p.UserId == "1");
        Assert.True(profile.Streak >= 1);
    }

    [Fact]
    public async Task DailyStreak_MoreThan5Updates_DoesNotDoubleCount()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var srs = new SrsService(db);

        // 10 answers in one day
        for (int i = 1; i <= 10; i++)
        {
            await srs.UpdateAsync("1", new SrsUpdateDto { QuestionId = i, IsCorrect = true, TimeMs = 1000 });
        }

        var profile = await db.UserProfiles.FirstAsync(p => p.UserId == "1");
        // Streak should still be 1 (one day completed, not two)
        Assert.Equal(1, profile.Streak);
    }

    [Fact]
    public async Task DailyStreak_WrongAnswersCount_TowardsDailyGoal()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var srs = new SrsService(db);

        // Mix of correct and wrong — all count towards daily goal
        for (int i = 1; i <= 5; i++)
        {
            await srs.UpdateAsync("1", new SrsUpdateDto
            {
                QuestionId = i,
                IsCorrect = i % 2 == 0,
                TimeMs = 1000
            });
        }

        var dailyStat = await db.UserDailyStats
            .FirstOrDefaultAsync(x => x.UserId == "1");

        Assert.NotNull(dailyStat);
        Assert.True(dailyStat.Completed);
    }
}
