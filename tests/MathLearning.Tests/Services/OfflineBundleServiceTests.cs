using MathLearning.Application.DTOs.Sync;
using MathLearning.Domain.Entities;
using MathLearning.Domain.Enums;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services.Sync;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MathLearning.Tests.Services;

public sealed class OfflineBundleServiceTests
{
    [Fact]
    public async Task GetBundleAsync_UsesResolvedLanguageAndMapsHintFullSeparately()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        await AddLocalizedContentAsync(db);

        db.UserSettings.Add(new UserSettings
        {
            UserId = "1",
            Language = "de",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var bundle = await service.GetBundleAsync("1", null, 1, "en-US", CancellationToken.None);

        var localizedQuestion = Assert.Single(bundle.Questions);
        var localizedOption = localizedQuestion.Options.Single(x => x.Text == "Deutsche Option");

        Assert.Equal("Deutsche Frage", localizedQuestion.Text);
        Assert.Equal("DE light", localizedQuestion.HintLight);
        Assert.Equal("DE medium", localizedQuestion.HintMedium);
        Assert.Equal("DE full", localizedQuestion.HintFull);
        Assert.Equal("DE explanation", localizedQuestion.Explanation);
        Assert.NotEqual(localizedQuestion.Explanation, localizedQuestion.HintFull);
        Assert.Equal("Question semantics override", localizedQuestion.SemanticsAltText);
        Assert.Equal("Deutsche Option", localizedOption.Text);
        Assert.Equal("Option semantics override", localizedOption.SemanticsAltText);
        Assert.False(string.IsNullOrWhiteSpace(bundle.Manifest.Version));
        Assert.False(string.IsNullOrWhiteSpace(bundle.Manifest.SnapshotVersion));
    }

    [Fact]
    public async Task GetBundleAsync_ContentVersionChangesWhenSerializedLearningContentChanges()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        await AddLocalizedContentAsync(db);

        db.UserSettings.Add(new UserSettings
        {
            UserId = "1",
            Language = "de",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var initial = await service.GetBundleAsync("1", null, 1, "en-US", CancellationToken.None);

        var stepTranslation = await db.QuestionStepTranslations.SingleAsync();
        stepTranslation.SetText("Deutscher Schritt 1");
        await db.SaveChangesAsync();

        var afterStepChange = await service.GetBundleAsync("1", null, 1, "en-US", CancellationToken.None);

        Assert.NotEqual(initial.Manifest.Version, afterStepChange.Manifest.Version);
        Assert.Equal(initial.Manifest.SnapshotVersion, afterStepChange.Manifest.SnapshotVersion);
    }

    [Fact]
    public async Task GetBundleAsync_SnapshotVersionChangesWhenOnlyUserProfileChanges()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        await AddLocalizedContentAsync(db);

        db.UserSettings.Add(new UserSettings
        {
            UserId = "1",
            Language = "de",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var initial = await service.GetBundleAsync("1", null, 1, "en-US", CancellationToken.None);

        var profile = await db.UserProfiles.SingleAsync(x => x.UserId == "1");
        profile.Xp += 50;
        profile.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var afterProfileChange = await service.GetBundleAsync("1", null, 1, "en-US", CancellationToken.None);

        Assert.Equal(initial.Manifest.Version, afterProfileChange.Manifest.Version);
        Assert.NotEqual(initial.Manifest.SnapshotVersion, afterProfileChange.Manifest.SnapshotVersion);
    }

    private static OfflineBundleService CreateService(ApiDbContext db)
        => new(db, Options.Create(new SyncOptions
        {
            DefaultQuestionBundleSize = 50
        }));

    private static async Task AddLocalizedContentAsync(ApiDbContext db)
    {
        var question = await db.Questions
            .Include(x => x.Options)
            .Include(x => x.Translations)
            .Include(x => x.Steps)
            .ThenInclude(x => x.Translations)
            .OrderBy(x => x.Difficulty)
            .ThenBy(x => x.Id)
            .FirstAsync();

        question.SetSemanticsAltText("Question semantics override");
        question.SetExplanation("Original explanation");
        question.SetHintFormula("Original light");
        question.SetHintClue("Original medium");
        question.SetHintFull("Original full");
        question.SetPublishState(QuestionPublishStates.Published, "test-user", DateTime.UtcNow);

        var correctOption = question.Options.Single(x => x.IsCorrect);
        correctOption.Update(
            "Original option",
            true,
            ContentFormat.PlainText,
            RenderMode.Auto,
            "Option semantics override",
            order: 1);
        correctOption.Translations.Add(new OptionTranslation(correctOption.Id, "de", "Deutsche Option"));

        question.Translations.Add(new QuestionTranslation(
            question.Id,
            "de",
            "Deutsche Frage",
            explanation: "DE explanation",
            hintLight: "DE light",
            hintMedium: "DE medium",
            hintFull: "DE full"));

        var step = new QuestionStep(
            question.Id,
            1,
            "Original step",
            hint: "Original step hint",
            highlight: false,
            textFormat: ContentFormat.PlainText,
            hintFormat: ContentFormat.PlainText,
            semanticsAltText: "Step semantics override");
        step.Translations.Add(new QuestionStepTranslation(step.Id, "de", "Deutscher Schritt", "DE step hint"));

        question.ReplaceSteps(new[] { step });
        await db.SaveChangesAsync();
    }
}
