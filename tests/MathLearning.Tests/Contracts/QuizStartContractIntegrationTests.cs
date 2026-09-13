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

        AssertPreAnswerQuestionArrayShape(payload.GetProperty("questions"), expectedCount, "Quiz hot path");
    }

    [Fact]
    public async Task QuizStart_EmptySubtopic_ReturnsNoPlayableQuestions()
    {
        var quizData = await SeedQuizPoolAsync("start-empty", 8, createEmptySubtopic: true);

        var response = await PostAsUserAsync("/api/quiz/start", new
        {
            subtopicId = quizData.EmptySubtopicId,
            questionCount = 10
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var payload = await ReadJsonAsync(response);
        Assert.Equal("NO_PLAYABLE_QUESTIONS", payload.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task PracticeStart_EmptySubtopic_ReturnsNoPlayableQuestionsAsNotFound()
    {
        var quizData = await SeedQuizPoolAsync("practice-empty", 8, createEmptySubtopic: true);

        var response = await PostAsUserAsync("/api/practice/session/start", new
        {
            skillNodeId = "practice-empty",
            topicId = (int?)null,
            subtopicId = quizData.EmptySubtopicId,
            targetQuestions = 10,
            preferredDifficulty = "medium"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var payload = await ReadJsonAsync(response);
        Assert.False(payload.GetProperty("success").GetBoolean());
        Assert.Equal("NO_PLAYABLE_QUESTIONS", payload.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task LegacyQuizQuestions_TopicKeySelectsQuestionsAcrossTopicSubtopics()
    {
        var quizData = await SeedQuizPoolAsync("legacy-topic-key", 2, createEmptySubtopic: true);
        int topicId;
        using (var scopeHandle = _factory.Services.CreateScope())
        {
            var db = scopeHandle.ServiceProvider.GetRequiredService<ApiDbContext>();
            topicId = await db.Subtopics
                .Where(x => x.Id == quizData.HotSubtopicId)
                .Select(x => x.TopicId)
                .SingleAsync();
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/quiz/questions?topic=topic_{topicId}&count=2");
        request.Headers.Add("X-Test-UserId", "1");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await ReadJsonAsync(response);
        AssertPreAnswerQuestionArrayShape(
            payload.GetProperty("questions"),
            2,
            "legacy-topic-key quiz hot path question");
    }

    [Fact]
    public async Task QuizStart_ExcludesDraftAndDeletedQuestions()
    {
        var quizData = await SeedQuizPoolAsync("start-playability", 1, createEmptySubtopic: true);

        using (var scopeHandle = _factory.Services.CreateScope())
        {
            var db = scopeHandle.ServiceProvider.GetRequiredService<ApiDbContext>();

            var draft = new Question("Draft question", 1, 1);
            draft.SetSubtopic(quizData.HotSubtopicId);
            draft.ReplaceOptions(new[]
            {
                new QuestionOption("Draft A", true, order: 1),
                new QuestionOption("Draft B", false, order: 2)
            });
            db.Questions.Add(draft);

            var deleted = new Question("Deleted question", 1, 1);
            deleted.SetSubtopic(quizData.HotSubtopicId);
            deleted.ReplaceOptions(new[]
            {
                new QuestionOption("Deleted A", true, order: 1),
                new QuestionOption("Deleted B", false, order: 2)
            });
            deleted.SetPublishState(QuestionPublishStates.Published, "test-fixture", DateTime.UtcNow);
            deleted.SoftDelete();
            db.Questions.Add(deleted);

            await db.SaveChangesAsync();
        }

        var response = await PostAsUserAsync("/api/quiz/start", new
        {
            subtopicId = quizData.HotSubtopicId,
            questionCount = 10
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await ReadJsonAsync(response);
        Assert.Equal(1, payload.GetProperty("questions").GetArrayLength());
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

        AssertPreAnswerQuestionArrayShape(payload.GetProperty("questions"), 25, "Quiz hot path");
    }

    [Fact]
    public async Task NextQuestion_ReturnsPreAnswerSafeShape()
    {
        var quizData = await SeedQuizPoolAsync("next-question", 4, createEmptySubtopic: true);

        var response = await PostAsUserAsync("/api/quiz/next-question", new
        {
            quizId = Guid.NewGuid(),
            subtopicId = quizData.HotSubtopicId
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await ReadJsonAsync(response);
        AssertPreAnswerQuestionShape(payload);
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
            question.SetPublishState(QuestionPublishStates.Published, "test-fixture", DateTime.UtcNow);

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

    private static void AssertPreAnswerQuestionArrayShape(JsonElement questionsElement, int expectedCount, string expectedTextFragment)
    {
        Assert.Equal(JsonValueKind.Array, questionsElement.ValueKind);

        var questions = questionsElement.EnumerateArray().ToList();
        Assert.Equal(expectedCount, questions.Count);

        Assert.All(questions, AssertPreAnswerQuestionShape);
        Assert.All(questions, question =>
        {
            Assert.True(question.TryGetProperty("text", out var textElement));
            Assert.Contains(expectedTextFragment, textElement.GetString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static void AssertPreAnswerQuestionShape(JsonElement question)
    {
        Assert.True(question.TryGetProperty("id", out var idElement));
        Assert.True(idElement.GetInt32() > 0);

        Assert.True(question.TryGetProperty("text", out var textElement));
        Assert.False(string.IsNullOrWhiteSpace(textElement.GetString()));

        Assert.True(question.TryGetProperty("options", out var optionsElement));
        Assert.Equal(JsonValueKind.Array, optionsElement.ValueKind);
        Assert.Equal(4, optionsElement.GetArrayLength());

        Assert.True(question.TryGetProperty("hintLight", out _));
        Assert.True(question.TryGetProperty("hintMedium", out _));
        Assert.False(question.TryGetProperty("correctAnswerId", out _));
        Assert.False(question.TryGetProperty("hintFull", out _));
        Assert.False(question.TryGetProperty("explanation", out _));
        Assert.False(question.TryGetProperty("steps", out _));
    }
}
