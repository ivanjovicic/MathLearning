using System.Net;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Endpoints;

public sealed class InlineLatexEndpointContractTests :
    IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> factory;
    private readonly HttpClient client;

    public InlineLatexEndpointContractTests(CustomWebApplicationFactory<Program> factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task LegacyQuestionsResponse_PreservesExistingInlineMathAcrossContentFields()
    {
        var subtopicId = await SeedDedicatedQuestionAsync();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/quiz/questions?subtopicId={subtopicId}&count=1");
        request.Headers.Add("X-Test-UserId", "1");
        request.Headers.Add("Accept-Language", "sr-RS");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        var questions = json.RootElement.GetProperty("questions");
        var question = Assert.Single(questions.EnumerateArray().ToArray());

        Assert.Equal(
            "Za $x=1$ i $f(x)=2x+3$, izračunaj vrednost.",
            question.GetProperty("text").GetString());
        Assert.Equal(
            "Vrednost je $5$.",
            Assert.Single(question.GetProperty("options").EnumerateArray().ToArray())
                .GetProperty("text")
                .GetString());
        Assert.Equal("Koristi $x=1$.", question.GetProperty("hintLight").GetString());
        Assert.Equal(
            "Pošto je $f(x)=2x+3$, važi $f(1)=5$.",
            question.GetProperty("explanation").GetString());

        Assert.DoesNotContain("Za  i", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Pošto je ,", body, StringComparison.Ordinal);
    }

    private async Task<int> SeedDedicatedQuestionAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        var topic = new Topic("Inline LaTeX contract", "Dedicated endpoint regression topic");
        db.Topics.Add(topic);
        await db.SaveChangesAsync();

        var subtopic = new Subtopic("Inline LaTeX contract", topic.Id);
        db.Subtopics.Add(subtopic);
        await db.SaveChangesAsync();

        var question = new Question(
            "Za $x=1$ i f(x)=2x+3, izračunaj vrednost.",
            difficulty: 1,
            categoryId: db.Categories.Select(category => category.Id).First(),
            explanation: "Pošto je $f(x)=2x+3$, važi $f(1)=5$.");
        question.SetSubtopic(subtopic.Id);
        question.SetHintFormula("Koristi $x=1$.");
        question.ReplaceOptions(new[]
        {
            new QuestionOption("Vrednost je $5$.", isCorrect: true)
        });
        db.Questions.Add(question);
        await db.SaveChangesAsync();

        return subtopic.Id;
    }
}
