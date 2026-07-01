using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Contracts;

public sealed class QuizStartContractIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public QuizStartContractIntegrationTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(3, 3)]
    [InlineData(999, 25)]
    public async Task QuizStart_ReturnsBoundedQuestionSetAndMobileContractShape(
        int requestedCount,
        int expectedCount)
    {
        var quizData = await SeedQuizPoolAsync("start-boundary", 30, createEmptySubtopic: true);

        var response = await PostAsUserAsync("/api/quiz/start", new
        {
            subtopicId = quizData.HotSubtopicId,
            questionCount = requestedCount
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await ReadJsonAsync(response);
        Assert.True(Guid.TryParse(payload.GetProperty("quizId").GetString(), out _));

        AssertQuizQuestionsShape(payload.GetProperty("questions"), expectedCount, "Quiz hot path");
    }

    [Fact]
    public async Task QuizStart_EmptySubtopic_ReturnsEmptyQuestionList()
    {
        var quizData = await SeedQuizPoolAsync("start-empty", 8, createEmptySubtopic: true);

        var response = await PostAsUserAsync("/api/quiz/start", new
        {
            subtopicId = quizData.EmptySubtopicId,
            questionCount = 10
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await ReadJsonAsync(response);
        Assert.True(Guid.TryParse(payload.GetProperty("quizId").GetString(), out _));
        Assert.Equal(0, payload.GetProperty("questions").GetArrayLength());
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task LegacyQuizQuestions_ClampCountAndPreserveShape(string method)
    {
        var quizData = await SeedQuizPoolAsync("legacy-boundary", 30, createEmptySubtopic: true);

        var response = await SendLegacyQuestionsRequestAsync(method, quizData.HotSubtopicId, 999);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await ReadJsonAsync(response);
        Assert.True(Guid.TryParse(payload.GetProperty("quizId").GetString(), out _));

        AssertQuizQuestionsShape(payload.GetProperty("questions"), 25, "Quiz hot path");
    }

    private async Task<(int HotSubtopicId, int EmptySubtopicId)> SeedQuizPoolAsync(
        string scope,
        int hotQuestionCount,
        bool createEmptySubtopic)
    {
        using var scopeHandle = _factory.Services.CreateScope();
        var db = scopeHandle.ServiceProvider.GetRequiredService<ApiDbContext>();

        var category = new Category($"{scope} category");
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var hotTopic = new Topic($"{scope} hot topic", $"{scope} hot topic description");
        db.Topics.Add(hotTopic);
        await db.SaveChangesAsync();

        var hotSubtopic = new Subtopic($"{scope} hot subtopic", hotTopic.Id);
        db.Subtopics.Add(hotSubtopic);
        await db.SaveChangesAsync();

        for (var i = 1; i <= hotQuestionCount; i++)
        {
            var question = new Question($"{scope} quiz hot path question {i}?", (i % 3) + 1, category.Id);
            question.SetSubtopic(hotSubtopic.Id);
            question.ReplaceOptions(new[]
            {
                new QuestionOption($"Answer {i} A", true, order: 1),
                new QuestionOption($"Answer {i} B", false, order: 2),
                new QuestionOption($"Answer {i} C", false, order: 3),
                new QuestionOption($"Answer {i} D", false, order: 4)
            });

            db.Questions.Add(question);
        }

        var emptyTopic = new Topic($"{scope} empty topic", $"{scope} empty topic description");
        db.Topics.Add(emptyTopic);
        await db.SaveChangesAsync();

        var emptySubtopic = new Subtopic($"{scope} empty subtopic", emptyTopic.Id);
        db.Subtopics.Add(emptySubtopic);

        await db.SaveChangesAsync();

        return (hotSubtopic.Id, createEmptySubtopic ? emptySubtopic.Id : hotSubtopic.Id);
    }

    private async Task<HttpResponseMessage> PostAsUserAsync(string url, object payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("X-Test-UserId", "1");
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendLegacyQuestionsRequestAsync(string method, int subtopicId, int count)
    {
        using var request = new HttpRequestMessage(
            new HttpMethod(method),
            method == HttpMethod.Get.Method
                ? $"/api/quiz/questions?subtopicId={subtopicId}&count={count}"
                : "/api/quiz/questions");
        request.Headers.Add("X-Test-UserId", "1");

        if (method != HttpMethod.Get.Method)
        {
            request.Content = JsonContent.Create(new
            {
                subtopicId,
                count
            });
        }

        return await _client.SendAsync(request);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return payload.ValueKind == JsonValueKind.Undefined
            ? throw new InvalidOperationException("Expected JSON response.")
            : payload;
    }

    private static void AssertQuizQuestionsShape(JsonElement questionsElement, int expectedCount, string expectedTextFragment)
    {
        Assert.Equal(JsonValueKind.Array, questionsElement.ValueKind);

        var questions = questionsElement.EnumerateArray().ToList();
        Assert.Equal(expectedCount, questions.Count);

        Assert.All(questions, question =>
        {
            Assert.True(question.TryGetProperty("id", out var idElement));
            Assert.True(idElement.GetInt32() > 0);

            Assert.True(question.TryGetProperty("text", out var textElement));
            Assert.False(string.IsNullOrWhiteSpace(textElement.GetString()));
            Assert.Contains(expectedTextFragment, textElement.GetString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);

            Assert.True(question.TryGetProperty("options", out var optionsElement));
            Assert.Equal(JsonValueKind.Array, optionsElement.ValueKind);
            Assert.Equal(4, optionsElement.GetArrayLength());

            Assert.True(question.TryGetProperty("correctAnswerId", out var correctAnswerIdElement));
            Assert.True(correctAnswerIdElement.GetInt32() > 0);
        });
    }
}
