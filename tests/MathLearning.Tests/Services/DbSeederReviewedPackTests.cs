using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Services;
using MathLearning.Infrastructure.Services.QuestionAuthoring;
using MathLearning.Tests.Helpers;

namespace MathLearning.Tests.Services;

public class DbSeederReviewedPackTests
{
    [Fact]
    public async Task StartupSeeder_DoesNotPromoteDesignatedUnreviewedQuestion()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync();
        var question = new Question("Koliko je 7 × 8?", 2, 1);
        question.SetSubtopic(1);
        question.ReplaceOptions([new QuestionOption("56", true, 0), new QuestionOption("55", false, 1)]);
        db.Questions.Add(question);
        await db.SaveChangesAsync();

        await DbSeeder.PublishPlayableQuestionsAsync(db, new MathVisualContentPolicy([], [1]));

        Assert.Equal(QuestionPublishStates.Draft, question.PublishState);
    }
}
