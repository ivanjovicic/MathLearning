using MathLearning.Application.DTOs.Quiz;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Services;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Identity;

namespace MathLearning.Tests.Services;

public class SrsServiceTests
{
    // ==========================================
    // 1. BASIC SRS UPDATE
    // ==========================================

    [Fact]
    public async Task UpdateAsync_NewQuestion_CreatesStatWithDefaults()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var srs = new SrsService(db);

        var result = await srs.UpdateAsync("1", new SrsUpdateDto
        {
            QuestionId = 1,
            IsCorrect = true,
            TimeMs = 3000
        });

        Assert.NotNull(result);
        Assert.Equal(1, result.UserId);
        Assert.Equal(1, result.QuestionId);
        Assert.Equal(1, result.SuccessStreak);
        Assert.NotNull(result.LastAnswered);
    }

    [Fact]
    public async Task UpdateAsync_ExistingQuestion_UpdatesStat()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var srs = new SrsService(db);

        await srs.UpdateAsync("1", new SrsUpdateDto { QuestionId = 1, IsCorrect = true, TimeMs = 2000 });
        var result = await srs.UpdateAsync("1", new SrsUpdateDto { QuestionId = 1, IsCorrect = true, TimeMs = 1500 });

        Assert.Equal(2, result.SuccessStreak);
    }

    // ==========================================
    // 2. STREAK TESTS
    // ==========================================

    [Fact]
    public async Task UpdateAsync_CorrectAnswer_IncreasesStreak()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var srs = new SrsService(db);

        await srs.UpdateAsync("1", new SrsUpdateDto { QuestionId = 1, IsCorrect = true, TimeMs = 1000 });
        await srs.UpdateAsync("1", new SrsUpdateDto { QuestionId = 2, IsCorrect = true, TimeMs = 1000 });
        var result = await srs.UpdateAsync("1", new SrsUpdateDto { QuestionId = 3, IsCorrect = true, TimeMs = 1000 });

        // Each question has its own streak
        Assert.Equal(1, result.SuccessStreak);
    }

    [Fact]
    public async Task UpdateAsync_WrongAnswer_ResetsStreak()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var srs = new SrsService(db);

        // Build streak
        await srs.UpdateAsync("1", new SrsUpdateDto { QuestionId = 1, IsCorrect = true, TimeMs = 1000 });
        await srs.UpdateAsync("1", new SrsUpdateDto { QuestionId = 1, IsCorrect = true, TimeMs = 1000 });
        await srs.UpdateAsync("1", new SrsUpdateDto { QuestionId = 1, IsCorrect = true, TimeMs = 1000 });

        // Wrong answer resets
        var result = await srs.UpdateAsync("1", new SrsUpdateDto { QuestionId = 1, IsCorrect = false, TimeMs = 5000 });

        Assert.Equal(0, result.SuccessStreak);
    }

    [Fact]
    public async Task UpdateAsync_MultipleCorrectThenWrongThenCorrect_StreakResetsAndRebuilds()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var srs = new SrsService(db);

        await srs.UpdateAsync("1", new SrsUpdateDto { QuestionId = 1, IsCorrect = true, TimeMs = 1000 });
        await srs.UpdateAsync("1", new SrsUpdateDto { QuestionId = 1, IsCorrect = true, TimeMs = 1000 });
        await srs.UpdateAsync("1", new SrsUpdateDto { QuestionId = 1, IsCorrect = false, TimeMs = 1000 });
        var result = await srs.UpdateAsync("1", new SrsUpdateDto { QuestionId = 1, IsCorrect = true, TimeMs = 1000 });

        Assert.Equal(1, result.SuccessStreak);
    }

    // ==========================================
    // 3. EASE TESTS
    // ==========================================

    [Fact]
    public async Task UpdateAsync_CorrectAnswer_IncreasesEase()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var srs = new SrsService(db);

        var result = await srs.UpdateAsync("1", new SrsUpdateDto { QuestionId = 1, IsCorrect = true, TimeMs = 1000 });

        Assert.True(result.Ease > 1.3);
        Assert.Equal(1.35, result.Ease, 2);
    }

    [Fact]
    public async Task UpdateAsync_WrongAnswer_DecreasesEase()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var srs = new SrsService(db);

        var result = await srs.UpdateAsync("1", new SrsUpdateDto { QuestionId = 1, IsCorrect = false, TimeMs = 5000 });

        Assert.True(result.Ease < 1.3);
        Assert.Equal(1.2, result.Ease, 2);
    }

    [Fact]
    public async Task UpdateAsync_EaseHasMaximum_CapsAt3()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var srs = new SrsService(db);

        // 40 correct in a row → ease should cap at 3.0
        for (int i = 0; i < 40; i++)
        {
            await srs.UpdateAsync("1", new SrsUpdateDto { QuestionId = 1, IsCorrect = true, TimeMs = 1000 });
        }

        var stat = db.QuestionStats.First(x => x.QuestionId == 1 && x.UserId == "1");
        Assert.True(stat.Ease <= 3.0);
    }

    [Fact]
    public async Task UpdateAsync_EaseHasMinimum_CapsAt1()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var srs = new SrsService(db);

        // Many wrong answers → ease should not go below 1.0
        for (int i = 0; i < 20; i++)
        {
            await srs.UpdateAsync("1", new SrsUpdateDto { QuestionId = 1, IsCorrect = false, TimeMs = 5000 });
        }

        var stat = db.QuestionStats.First(x => x.QuestionId == 1 && x.UserId == "1");
        Assert.True(stat.Ease >= 1.0);
    }

    // ==========================================
    // 4. NEXT REVIEW CALCULATION
    // ==========================================

    [Fact]
    public async Task UpdateAsync_FirstCorrect_NextReviewIsAbout1Day()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var srs = new SrsService(db);
        var before = DateTime.UtcNow;

        var result = await srs.UpdateAsync("1", new SrsUpdateDto { QuestionId = 1, IsCorrect = true, TimeMs = 1000 });

        // baseIntervals[1] = 2, ease ~1.35 → ~2.7 days
        // streak=1, index=min(1,4)=1 → 2 * 1.35 = 2.7 days
        var expectedMin = before.AddDays(2.0);
        var expectedMax = before.AddDays(3.5);

        Assert.True(result.NextReview >= expectedMin, $"NextReview {result.NextReview} should be >= {expectedMin}");
        Assert.True(result.NextReview <= expectedMax, $"NextReview {result.NextReview} should be <= {expectedMax}");
    }

    [Fact]
    public async Task UpdateAsync_WrongAnswer_NextReviewIsSoon()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var srs = new SrsService(db);
        var before = DateTime.UtcNow;

        var result = await srs.UpdateAsync("1", new SrsUpdateDto { QuestionId = 1, IsCorrect = false, TimeMs = 5000 });

        // streak=0, index=0 → baseIntervals[0]=1 * ease(1.2) = 1.2 days
        var expectedMax = before.AddDays(2);

        Assert.True(result.NextReview <= expectedMax);
    }

    [Fact]
    public async Task UpdateAsync_HighStreak_NextReviewIsFarInFuture()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var srs = new SrsService(db);

        QuestionStat result = null!;
        for (int i = 0; i < 6; i++)
        {
            result = await srs.UpdateAsync("1", new SrsUpdateDto { QuestionId = 1, IsCorrect = true, TimeMs = 1000 });
        }

        // streak=6, capped at index 4 → baseIntervals[4]=15 * ease(~1.6) = ~24 days
        Assert.True(result.NextReview > DateTime.UtcNow.AddDays(10));
    }

    // ==========================================
    // 5. LAST ANSWERED
    // ==========================================

    [Fact]
    public async Task UpdateAsync_SetsLastAnswered()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var srs = new SrsService(db);
        var before = DateTime.UtcNow;

        var result = await srs.UpdateAsync("1", new SrsUpdateDto { QuestionId = 1, IsCorrect = true, TimeMs = 1000 });

        Assert.NotNull(result.LastAnswered);
        Assert.True(result.LastAnswered >= before);
        Assert.True(result.LastAnswered <= DateTime.UtcNow.AddSeconds(5));
    }

    // ==========================================
    // 6. DIFFERENT USERS
    // ==========================================

    [Fact]
    public async Task UpdateAsync_DifferentUsers_IndependentStats()
    {
        var dbName = Guid.NewGuid().ToString();
        var db = await TestDbContextFactory.CreateWithSeedAsync(dbName);

        // Add second user profile
        db.Users.Add(new IdentityUser { Id = "2", UserName = "user2", Email = "user2@example.com" });
        db.UserProfiles.Add(new UserProfile
        {
            UserId = "2", Username = "user2", DisplayName = "User Two",
            Coins = 100, Level = 1, Xp = 0, Streak = 0,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var srs = new SrsService(db);

        var r1 = await srs.UpdateAsync("1", new SrsUpdateDto { QuestionId = 1, IsCorrect = true, TimeMs = 1000 });
        var r2 = await srs.UpdateAsync("2", new SrsUpdateDto { QuestionId = 1, IsCorrect = false, TimeMs = 5000 });

        Assert.Equal(1, r1.SuccessStreak);
        Assert.Equal(0, r2.SuccessStreak);
        Assert.True(r1.Ease > r2.Ease);
    }
}
