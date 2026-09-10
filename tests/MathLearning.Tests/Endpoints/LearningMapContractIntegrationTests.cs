using System.Net;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Application.DTOs.Adaptive;
using MathLearning.Application.DTOs.Analytics;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MathLearning.Tests.Endpoints;

public sealed class LearningMapContractIntegrationTests :
    IClassFixture<LearningMapContractWebApplicationFactory>
{
    private readonly LearningMapContractWebApplicationFactory factory;
    private readonly HttpClient client;

    public LearningMapContractIntegrationTests(LearningMapContractWebApplicationFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task NewUser_GetsValidEmptyLearningMapWithoutSyntheticNodes()
    {
        factory.Adaptive.SetLearningMap("new-user", EmptyMap("not_enough_learning_data"));

        var response = await client.SendAsync(AuthHeaders("/api/adaptive/path", "new-user"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(0, json.RootElement.GetProperty("nodes").GetArrayLength());
        Assert.Equal(0, json.RootElement.GetProperty("edges").GetArrayLength());
        Assert.True(json.RootElement.GetProperty("recommendedNext").ValueKind is JsonValueKind.Null);
        Assert.Equal("not_enough_learning_data", json.RootElement.GetProperty("emptyReason").GetString());
        Assert.True(json.RootElement.GetProperty("generatedAt").ValueKind is JsonValueKind.String);
    }

    [Fact]
    public async Task UserWithProgress_GetsRequiredLearningMapNodeFields()
    {
        var node = new LearningMapNodeDto(
            "topic-7-subtopic-9",
            "Linear equations",
            "Algebra",
            7,
            9,
            0.72,
            false,
            AdaptiveDifficultyLevels.Medium);
        factory.Adaptive.SetLearningMap("progress-user", new LearningMapDto(
            new[] { node },
            Array.Empty<LearningMapEdgeDto>(),
            node.Id,
            DateTime.UtcNow));

        var response = await client.SendAsync(AuthHeaders("/api/adaptive/path", "progress-user"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var actual = json.RootElement.GetProperty("nodes")[0];
        Assert.Equal(node.Id, actual.GetProperty("id").GetString());
        Assert.Equal(node.Title, actual.GetProperty("title").GetString());
        Assert.Equal(node.TopicId, actual.GetProperty("topicId").GetInt32());
        Assert.Equal(node.SubtopicId, actual.GetProperty("subtopicId").GetInt32());
        Assert.Equal(node.Mastery, actual.GetProperty("mastery").GetDouble());
        Assert.Equal(node.IsLocked, actual.GetProperty("isLocked").GetBoolean());
        Assert.Equal(node.RecommendedDifficulty, actual.GetProperty("recommendedDifficulty").GetString());
    }

    [Fact]
    public async Task LearningMap_ServiceFailure_ReturnsSafe500()
    {
        var response = await client.SendAsync(AuthHeaders("/api/adaptive/path", "failure-user"));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("learning-map-secret", body);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("INTERNAL_ERROR", json.RootElement.GetProperty("errorCode").GetString());

    }

    [Fact]
    public async Task LearningMap_IsolatedByAuthenticatedUser()
    {
        factory.Adaptive.SetLearningMap("user-a", MapFor("topic-a"));
        factory.Adaptive.SetLearningMap("user-b", MapFor("topic-b"));

        var responseA = await client.SendAsync(AuthHeaders("/api/adaptive/path?userId=user-b", "user-a"));
        var responseB = await client.SendAsync(AuthHeaders("/api/adaptive/path?userId=user-a", "user-b"));

        Assert.Equal("topic-a", await ReadFirstNodeIdAsync(responseA));
        Assert.Equal("topic-b", await ReadFirstNodeIdAsync(responseB));
    }

    [Fact]
    public async Task Mastery_EmptyResultIs200AndScopedToAuthenticatedUser()
    {
        factory.Adaptive.SetMastery("mastery-empty", Array.Empty<MasteryDto>());
        factory.Adaptive.SetMastery("mastery-user", new[]
        {
            new MasteryDto(7, "Algebra", 0.82)
        });

        var emptyResponse = await client.SendAsync(AuthHeaders("/api/analytics/mastery", "mastery-empty"));
        var populatedResponse = await client.SendAsync(
            AuthHeaders("/api/analytics/mastery?userId=mastery-empty", "mastery-user"));

        Assert.Equal(HttpStatusCode.OK, emptyResponse.StatusCode);
        using var emptyJson = JsonDocument.Parse(await emptyResponse.Content.ReadAsStringAsync());
        Assert.Equal(0, emptyJson.RootElement.GetArrayLength());

        Assert.Equal(HttpStatusCode.OK, populatedResponse.StatusCode);
        using var populatedJson = JsonDocument.Parse(await populatedResponse.Content.ReadAsStringAsync());
        Assert.Equal("Algebra", populatedJson.RootElement[0].GetProperty("topicName").GetString());
        Assert.Equal(0.82, populatedJson.RootElement[0].GetProperty("masteryProbability").GetDouble());
    }

    private static HttpRequestMessage AuthHeaders(string path, string userId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Test-UserId", userId);
        return request;
    }

    private static LearningMapDto EmptyMap(string reason) => new(
        Array.Empty<LearningMapNodeDto>(),
        Array.Empty<LearningMapEdgeDto>(),
        null,
        DateTime.UtcNow,
        reason);

    private static LearningMapDto MapFor(string id) => new(
        new[] { new LearningMapNodeDto(id, id, id, 1, 1, 0.5, false, AdaptiveDifficultyLevels.Medium) },
        Array.Empty<LearningMapEdgeDto>(),
        id,
        DateTime.UtcNow);

    private static async Task<string?> ReadFirstNodeIdAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("nodes")[0].GetProperty("id").GetString();
    }
}

public sealed class LearningMapContractWebApplicationFactory : CustomWebApplicationFactory<Program>
{
    public RecordingAdaptiveLearningService Adaptive { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAdaptiveLearningService>();
            services.AddSingleton<IAdaptiveLearningService>(Adaptive);
        });
    }
}

public sealed class RecordingAdaptiveLearningService : IAdaptiveLearningService
{
    private readonly Dictionary<string, LearningMapDto> maps = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<MasteryDto>> masteries = new(StringComparer.Ordinal);

    public void SetLearningMap(string userId, LearningMapDto map) => maps[userId] = map;

    public void SetMastery(string userId, IReadOnlyList<MasteryDto> values) => masteries[userId] = values;

    public Task<LearningMapDto> GetLearningMapAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.Equals(userId, "failure-user", StringComparison.Ordinal))
            throw new InvalidOperationException("learning-map-secret");

        return Task.FromResult(maps.GetValueOrDefault(userId) ?? EmptyMap());
    }

    public Task<IReadOnlyList<MasteryDto>> GetMasteryAsync(string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(masteries.GetValueOrDefault(userId) ?? Array.Empty<MasteryDto>());

    public Task<AdaptiveSession> GeneratePracticeSessionAsync(string userId) => throw new NotSupportedException();

    public Task<AdaptiveAnswerSubmissionResult> SubmitAnswerAsync(
        string userId,
        AdaptiveAnswerRequest request,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<List<AdaptiveRecommendation>> GetRecommendationsAsync(string userId) =>
        Task.FromResult(new List<AdaptiveRecommendation>());

    public Task<List<ReviewItem>> GetDueReviewsAsync(string userId) =>
        Task.FromResult(new List<ReviewItem>());

    public Task DetectWeakTopicsAsync(string userId) => Task.CompletedTask;

    private static LearningMapDto EmptyMap() => new(
        Array.Empty<LearningMapNodeDto>(),
        Array.Empty<LearningMapEdgeDto>(),
        null,
        DateTime.UtcNow,
        "not_enough_learning_data");
}
