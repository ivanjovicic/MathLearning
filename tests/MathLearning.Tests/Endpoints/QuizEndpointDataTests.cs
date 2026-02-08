using MathLearning.Domain.Entities;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Tests.Endpoints;

public class QuizEndpointDataTests
{
    // ==========================================
    // QUIZ START — data layer tests
    // ==========================================

    [Fact]
    public async Task QuizSession_CanBeCreated()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        var session = new QuizSession
        {
            Id = Guid.NewGuid(),
            UserId = 1,
            StartedAt = DateTime.UtcNow
        };
        db.QuizSessions.Add(session);
        await db.SaveChangesAsync();

        var found = await db.QuizSessions.FirstOrDefaultAsync(s => s.UserId == 1);
        Assert.NotNull(found);
    }

    // ==========================================
    // SUBMIT ANSWER — validation tests
    // ==========================================

    [Fact]
    public async Task SubmitAnswer_CorrectMultipleChoice_IsCorrect()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        var question = await db.Questions
            .Include(q => q.Options)
            .FirstAsync(q => q.Id == 1);

        var correctOption = question.Options.First(o => o.IsCorrect);

        bool isCorrect = question.Type == "multiple_choice"
            && question.Options.Any(o => o.IsCorrect && o.Text == correctOption.Text);

        Assert.True(isCorrect);
    }

    [Fact]
    public async Task SubmitAnswer_WrongMultipleChoice_IsIncorrect()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        var question = await db.Questions
            .Include(q => q.Options)
            .FirstAsync(q => q.Id == 1);

        var wrongOption = question.Options.First(o => !o.IsCorrect);

        bool isCorrect = question.Type == "multiple_choice"
            && question.Options.Any(o => o.IsCorrect && o.Text == wrongOption.Text);

        Assert.False(isCorrect);
    }

    [Fact]
    public async Task UserAnswer_CanBeSaved()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        var session = new QuizSession
        {
            Id = Guid.NewGuid(),
            UserId = 1,
            StartedAt = DateTime.UtcNow
        };
        db.QuizSessions.Add(session);

        db.UserAnswers.Add(new UserAnswer
        {
            UserId = 1,
            QuestionId = 1,
            QuizSessionId = session.Id,
            Answer = "2",
            IsCorrect = true,
            TimeSpentSeconds = 5,
            AnsweredAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var answer = await db.UserAnswers.FirstAsync(a => a.UserId == 1);
        Assert.True(answer.IsCorrect);
        Assert.Equal("2", answer.Answer);
    }

    // ==========================================
    // USER QUESTION STATS — update pattern
    // ==========================================

    [Fact]
    public async Task UserQuestionStats_CreateOrUpdate()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        // Create
        var stat = new UserQuestionStat
        {
            UserId = 1,
            QuestionId = 1,
            Attempts = 1,
            CorrectAttempts = 1,
            LastAttemptAt = DateTime.UtcNow
        };
        db.UserQuestionStats.Add(stat);
        await db.SaveChangesAsync();

        // Update
        stat.Attempts++;
        stat.CorrectAttempts++;
        await db.SaveChangesAsync();

        var found = await db.UserQuestionStats
            .FirstAsync(s => s.UserId == 1 && s.QuestionId == 1);
        Assert.Equal(2, found.Attempts);
        Assert.Equal(2, found.CorrectAttempts);
    }

    // ==========================================
    // SRS DAILY — data query tests
    // ==========================================

    [Fact]
    public async Task SrsDaily_ReturnsDueQuestions()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        // Add a stat with NextReview in the past → due
        db.QuestionStats.Add(new QuestionStat
        {
            UserId = 1,
            QuestionId = 1,
            NextReview = DateTime.UtcNow.AddDays(-1),
            Ease = 1.3,
            SuccessStreak = 2
        });

        // Add a stat with NextReview in the future → not due
        db.QuestionStats.Add(new QuestionStat
        {
            UserId = 1,
            QuestionId = 2,
            NextReview = DateTime.UtcNow.AddDays(5),
            Ease = 1.5,
            SuccessStreak = 3
        });
        await db.SaveChangesAsync();

        var dueQuestions = await db.QuestionStats
            .Where(x => x.UserId == 1 && x.NextReview <= DateTime.UtcNow)
            .OrderBy(x => x.Ease)
            .ToListAsync();

        Assert.Single(dueQuestions);
        Assert.Equal(1, dueQuestions[0].QuestionId);
    }

    // ==========================================
    // SRS MIXED — data query tests
    // ==========================================

    [Fact]
    public async Task SrsMixed_FillsWithRandomIfNotEnoughDue()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        // Only 2 due questions
        db.QuestionStats.Add(new QuestionStat
        {
            UserId = 1, QuestionId = 1,
            NextReview = DateTime.UtcNow.AddDays(-1), Ease = 1.3
        });
        db.QuestionStats.Add(new QuestionStat
        {
            UserId = 1, QuestionId = 2,
            NextReview = DateTime.UtcNow.AddDays(-2), Ease = 1.1
        });
        await db.SaveChangesAsync();

        int count = 5;

        var dueStats = await db.QuestionStats
            .Where(x => x.UserId == 1 && x.NextReview <= DateTime.UtcNow)
            .OrderBy(x => x.Ease)
            .Take(count)
            .ToListAsync();

        var dueIds = dueStats.Select(x => x.QuestionId).ToList();
        Assert.Equal(2, dueIds.Count);

        int needed = count - dueIds.Count;
        var randomQuestions = await db.Questions
            .Where(x => !dueIds.Contains(x.Id))
            .OrderBy(x => Guid.NewGuid())
            .Take(needed)
            .ToListAsync();

        Assert.Equal(3, randomQuestions.Count);
        Assert.Equal(5, dueIds.Count + randomQuestions.Count);
    }

    [Fact]
    public async Task SrsMixed_AllDue_NoRandomNeeded()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        // Add 5 due questions
        for (int i = 1; i <= 5; i++)
        {
            db.QuestionStats.Add(new QuestionStat
            {
                UserId = 1, QuestionId = i,
                NextReview = DateTime.UtcNow.AddDays(-1), Ease = 1.3
            });
        }
        await db.SaveChangesAsync();

        int count = 5;
        var dueStats = await db.QuestionStats
            .Where(x => x.UserId == 1 && x.NextReview <= DateTime.UtcNow)
            .Take(count)
            .ToListAsync();

        Assert.Equal(5, dueStats.Count);

        int needed = count - dueStats.Count;
        Assert.Equal(0, needed);
    }
}
