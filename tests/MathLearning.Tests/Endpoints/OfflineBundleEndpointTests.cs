using System.Net.Http.Headers;
using System.Text.Json;
using MathLearning.Domain.Entities;
using MathLearning.Domain.Enums;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Endpoints;

public sealed class OfflineBundleEndpointTests
{
    [Fact]
    public async Task GetOfflineBundle_ReturnsManifestAndLocalizedQuestionPayload()
    {
        await using var factory = new CustomWebApplicationFactory<Program>();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        var question = await db.Questions
            .Include(x => x.Options)
            .Include(x => x.Translations)
            .Include(x => x.Steps)
            .ThenInclude(x => x.Translations)
            .OrderBy(x => x.Difficulty)
            .ThenBy(x => x.Id)
            .FirstAsync();

        question.SetSemanticsAltText("Question semantics override");
        question.SetHintFormula("Original light");
        question.SetHintClue("Original medium");
        question.SetHintFull("Original full");
        question.SetExplanation("Original explanation");
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

        db.UserSettings.Add(new UserSettings
        {
            UserId = "test-user",
            Language = "de",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", "test-user");
        client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US"));

        var response = await client.GetAsync("/api/offline/bundle?questionCount=1");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var manifest = root.GetProperty("manifest");
        var questions = root.GetProperty("questions");
        var firstQuestion = questions.EnumerateArray().Single();

        Assert.True(manifest.TryGetProperty("version", out var versionProperty));
        Assert.True(manifest.TryGetProperty("snapshotVersion", out var snapshotVersionProperty));
        Assert.False(string.IsNullOrWhiteSpace(versionProperty.GetString()));
        Assert.False(string.IsNullOrWhiteSpace(snapshotVersionProperty.GetString()));
        Assert.Equal("Deutsche Frage", firstQuestion.GetProperty("text").GetString());
        Assert.Equal("DE full", firstQuestion.GetProperty("hintFull").GetString());
        Assert.Equal("DE explanation", firstQuestion.GetProperty("explanation").GetString());
    }
}
