using MathLearning.Domain.Entities;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Tests.Models;

public class QuestionTranslationModelTests
{
    [Fact]
    public async Task QuestionTranslation_CanBeCreatedWithHintLevels()
    {
        var db = TestDbContextFactory.Create();

        var translation = new QuestionTranslation(
            questionId: 1,
            lang: "sr",
            text: "Koliko je 2 + 2?",
            explanation: "Osnovno sabiranje",
            hintLight: "Pokušaj da dodaš brojeve",
            hintMedium: "2 + 2 = 4",
            hintFull: "Odgovor je 4"
        );

        db.QuestionTranslations.Add(translation);
        await db.SaveChangesAsync();

        var found = await db.QuestionTranslations.FirstAsync(t => t.QuestionId == 1);
        Assert.Equal("sr", found.Lang);
        Assert.Equal("Koliko je 2 + 2?", found.Text);
        Assert.Equal("Osnovno sabiranje", found.Explanation);
        Assert.Equal("Pokušaj da dodaš brojeve", found.HintLight);
        Assert.Equal("2 + 2 = 4", found.HintMedium);
        Assert.Equal("Odgovor je 4", found.HintFull);
    }

    [Fact]
    public async Task QuestionTranslation_DefaultValues()
    {
        var translation = new QuestionTranslation(1, "en", "What is 2 + 2?");

        Assert.Equal(1, translation.QuestionId);
        Assert.Equal("en", translation.Lang);
        Assert.Equal("What is 2 + 2?", translation.Text);
        Assert.Null(translation.Explanation);
        Assert.Null(translation.HintLight);
        Assert.Null(translation.HintMedium);
        Assert.Null(translation.HintFull);
    }

    [Fact]
    public async Task QuestionTranslation_CanUpdateHintLevels()
    {
        var db = TestDbContextFactory.Create();

        var translation = new QuestionTranslation(1, "sr", "Koliko je 2 + 2?");
        db.QuestionTranslations.Add(translation);
        await db.SaveChangesAsync();

        var found = await db.QuestionTranslations.FirstAsync(t => t.QuestionId == 1);
        found.SetHintLight("Lako");
        found.SetHintMedium("Srednje");
        found.SetHintFull("Teško");
        found.SetExplanation("Objašnjenje");
        await db.SaveChangesAsync();

        var updated = await db.QuestionTranslations.FirstAsync(t => t.QuestionId == 1);
        Assert.Equal("Lako", updated.HintLight);
        Assert.Equal("Srednje", updated.HintMedium);
        Assert.Equal("Teško", updated.HintFull);
        Assert.Equal("Objašnjenje", updated.Explanation);
    }
}
