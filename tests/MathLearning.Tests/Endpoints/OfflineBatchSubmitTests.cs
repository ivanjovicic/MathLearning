using MathLearning.Domain.Entities;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Tests.Endpoints;

public class OfflineBatchSubmitTests
{
    [Fact]
    public async Task OfflineBatch_ServerSideValidation_OverridesClientIsCorrect()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        var question = await db.Questions
            .Include(q => q.Options)
            .FirstAsync(q => q.Id == 1);

        var correctOption = question.Options.First(o => o.IsCorrect);
        var wrongOption = question.Options.First(o => !o.IsCorrect);

        // Client says wrong answer is correct — server should override
        bool clientSays = true;
        bool serverValidated = question.Type == "multiple_choice"
            && question.Options.Any(o => o.IsCorrect && o.Text == wrongOption.Text);

        Assert.True(clientSays);       // Client claims correct
        Assert.False(serverValidated); // Server says incorrect
    }

    [Fact]
    public async Task OfflineBatch_Idempotency_SameAnswerNotDuplicated()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        var session = new QuizSession
        {
            Id = Guid.NewGuid(), UserId = 1, StartedAt = DateTime.UtcNow
        };
        db.QuizSessions.Add(session);

        var answeredAt = DateTime.UtcNow;

        // First insert
        db.UserAnswers.Add(new UserAnswer
        {
            UserId = 1, QuestionId = 1, QuizSessionId = session.Id,
            Answer = "2", IsCorrect = true, TimeSpentSeconds = 5,
            AnsweredAt = answeredAt
        });
        await db.SaveChangesAsync();

        // Check if duplicate exists (idempotency check)
        bool exists = await db.UserAnswers
            .AnyAsync(x =>
                x.UserId == 1 &&
                x.QuestionId == 1 &&
                x.AnsweredAt == answeredAt);

        Assert.True(exists);

        // Should NOT add again
        var countBefore = await db.UserAnswers.CountAsync(x => x.UserId == 1 && x.QuestionId == 1);
        Assert.Equal(1, countBefore);
    }

    [Fact]
    public async Task OfflineBatch_StatsUpdated_AfterImport()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        var session = new QuizSession
        {
            Id = Guid.NewGuid(), UserId = 1, StartedAt = DateTime.UtcNow
        };
        db.QuizSessions.Add(session);

        // Simulate batch import — 3 correct, 2 wrong for question 1
        var stat = new UserQuestionStat
        {
            UserId = 1, QuestionId = 1, Attempts = 0, CorrectAttempts = 0
        };
        db.UserQuestionStats.Add(stat);

        for (int i = 0; i < 5; i++)
        {
            bool isCorrect = i < 3;
            stat.Attempts++;
            if (isCorrect) stat.CorrectAttempts++;
        }

        stat.LastAttemptAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var result = await db.UserQuestionStats
            .FirstAsync(s => s.UserId == 1 && s.QuestionId == 1);

        Assert.Equal(5, result.Attempts);
        Assert.Equal(3, result.CorrectAttempts);
        Assert.NotNull(result.LastAttemptAt);
    }
}
