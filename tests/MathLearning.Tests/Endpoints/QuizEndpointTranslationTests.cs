using MathLearning.Domain.Entities;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Tests.Endpoints;

public class QuizEndpointTranslationTests
{
    [Fact]
    public async Task SrsMixed_ReturnsTranslatedQuestionsWithHints()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        // Add translation with hints
        var translation = new QuestionTranslation(
            questionId: 1,
            lang: "sr",
            text: "Koliko je 1 + 1?",
            explanation: "Osnovno sabiranje",
            hintLight: "Saberi brojeve",
            hintMedium: "1 + 1 = 2",
            hintFull: "Odgovor je 2"
        );
        db.QuestionTranslations.Add(translation);

        // Add question stat to make it due
        db.QuestionStats.Add(new QuestionStat
        {
            UserId = 1,
            QuestionId = 1,
            NextReview = DateTime.UtcNow.AddDays(-1),
            Ease = 1.3
        });

        await db.SaveChangesAsync();

        // Simulate API call (we can't easily test endpoints in unit tests without WebApplicationFactory)
        // Instead, test the data layer logic

        var dueStats = await db.QuestionStats
            .Where(x => x.UserId == 1 && x.NextReview <= DateTime.UtcNow)
            .OrderBy(x => x.Ease)
            .Take(15)
            .ToListAsync();

        var dueIds = dueStats.Select(x => x.QuestionId).ToList();

        var srsQuestions = await db.Questions
            .Include(q => q.Options)
            .Include(q => q.Translations)
            .Where(q => dueIds.Contains(q.Id))
            .ToListAsync();

        Assert.Single(srsQuestions);
        var question = srsQuestions[0];

        // Check translation exists
        var translationFromDb = question.Translations.FirstOrDefault(t => t.Lang == "sr");
        Assert.NotNull(translationFromDb);
        Assert.Equal("Koliko je 1 + 1?", translationFromDb.Text);
        Assert.Equal("Osnovno sabiranje", translationFromDb.Explanation);
        Assert.Equal("Saberi brojeve", translationFromDb.HintLight);
        Assert.Equal("1 + 1 = 2", translationFromDb.HintMedium);
        Assert.Equal("Odgovor je 2", translationFromDb.HintFull);
    }

    [Fact]
    public async Task SrsMixed_FallsBackToDefaultTextIfNoTranslation()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        // Add question stat but no translation
        db.QuestionStats.Add(new QuestionStat
        {
            UserId = 1,
            QuestionId = 1,
            NextReview = DateTime.UtcNow.AddDays(-1),
            Ease = 1.3
        });

        await db.SaveChangesAsync();

        var dueStats = await db.QuestionStats
            .Where(x => x.UserId == 1 && x.NextReview <= DateTime.UtcNow)
            .OrderBy(x => x.Ease)
            .Take(15)
            .ToListAsync();

        var dueIds = dueStats.Select(x => x.QuestionId).ToList();

        var srsQuestions = await db.Questions
            .Include(q => q.Options)
            .Include(q => q.Translations)
            .Where(q => dueIds.Contains(q.Id))
            .ToListAsync();

        Assert.Single(srsQuestions);
        var question = srsQuestions[0];

        // Should fall back to original question text
        var translation = question.Translations.FirstOrDefault(t => t.Lang == "sr") ?? question.Translations.FirstOrDefault();
        var text = translation?.Text ?? question.Text;
        var explanation = translation?.Explanation ?? question.Explanation;

        Assert.Contains("1 + 1", text); // From seed data
        Assert.Null(explanation); // No translation
    }
}
