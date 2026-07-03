using System.Net;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Application.DTOs.Analytics;
using MathLearning.Application.Helpers;
using MathLearning.Application.Services;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MathLearning.Tests.Endpoints;

public sealed class AnalyticsEndpointContractTests :
    IClassFixture<AnalyticsContractWebApplicationFactory>,
    IAsyncLifetime
{
    private const string SecretFailure = "SECRET_ANALYTICS_SERVICE_FAILURE";

    private readonly AnalyticsContractWebApplicationFactory factory;
    private readonly HttpClient client;

    public AnalyticsEndpointContractTests(AnalyticsContractWebApplicationFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    public Task InitializeAsync()
    {
        factory.Service.Reset();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("/api/analytics/weakness")]
    [InlineData("/api/analytics/weakness/details")]
    [InlineData("/api/recommendations/practice")]
    public async Task AnonymousUser_IsDeniedBeforeAnalyticsServiceIsCalled(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(TestAuthHandler.AnonymousHeader, "true");

        var response = await client.SendAsync(request);

        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden);
        Assert.Equal(0, factory.Service.TotalCalls);
    }

    [Fact]
    public async Task Weakness_UsesAuthenticatedUserAndNormalizesPaging()
    {
        factory.Service.Topics = Enumerable.Range(1, 60)
            .Select(CreateTopic)
            .ToList();
        var authenticatedUser = "analytics-user-a";

        using var request = AuthenticatedGet(
            "/api/analytics/weakness?page=0&pageSize=999&userId=someone-else",
            authenticatedUser);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(UserIdGuidMapper.FromIdentityUserId(authenticatedUser), factory.Service.LastUserId);
        Assert.Equal(50, factory.Service.LastTake);
        Assert.True(factory.Service.LastCancellationToken.CanBeCanceled);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, json.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(50, json.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(50, json.RootElement.GetProperty("returned").GetInt32());
        Assert.Equal(50, json.RootElement.GetProperty("weakTopics").GetArrayLength());
        Assert.Equal(1, json.RootElement.GetProperty("weakTopics")[0].GetProperty("topicId").GetInt32());
    }

    [Fact]
    public async Task WeaknessDetails_PagesTopicsAndSubtopicsWithStableShape()
    {
        factory.Service.Topics = Enumerable.Range(1, 4).Select(CreateTopic).ToList();
        factory.Service.Subtopics = Enumerable.Range(1, 4).Select(CreateSubtopic).ToList();

        using var request = AuthenticatedGet(
            "/api/analytics/weakness/details?page=2&pageSize=2",
            "analytics-user-b");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(4, factory.Service.LastTopicTake);
        Assert.Equal(4, factory.Service.LastSubtopicTake);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, json.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("returnedTopics").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("returnedSubtopics").GetInt32());
        Assert.Equal(3, json.RootElement.GetProperty("weakTopics")[0].GetProperty("topicId").GetInt32());
        Assert.Equal(3, json.RootElement.GetProperty("weakSubtopics")[0].GetProperty("subtopicId").GetInt32());
        Assert.True(json.RootElement.GetProperty("weakTopics")[0].TryGetProperty("weaknessScore", out _));
        Assert.True(json.RootElement.GetProperty("weakSubtopics")[0].TryGetProperty("topicName", out _));
    }

    [Fact]
    public async Task PracticeRecommendations_UsesAuthenticatedUserAndReturnsRequestedPage()
    {
        factory.Service.Recommendations = Enumerable.Range(1, 4)
            .Select(i => new PracticeRecommendationDto(
                $"recommendation-{i}",
                $"Practice {i}",
                i,
                i,
                $"Reason {i}",
                1m / i))
            .ToList();
        var authenticatedUser = "analytics-user-c";

        using var request = AuthenticatedGet(
            "/api/recommendations/practice?page=2&pageSize=2&userId=forged-user",
            authenticatedUser);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(UserIdGuidMapper.FromIdentityUserId(authenticatedUser), factory.Service.LastUserId);
        Assert.Equal(4, factory.Service.LastRecommendationTake);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, json.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("returned").GetInt32());
        Assert.Equal("recommendation-3", json.RootElement.GetProperty("recommendations")[0].GetProperty("id").GetString());
    }

    [Fact]
    public async Task AnalyticsServiceFailure_ReturnsGenericErrorWithoutInternalMessage()
    {
        factory.Service.ExceptionToThrow = new InvalidOperationException(SecretFailure);
        using var request = AuthenticatedGet("/api/analytics/weakness", "analytics-failure-user");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(SecretFailure, body);

        using var json = JsonDocument.Parse(body);
        Assert.Equal("An unexpected error occurred.", json.RootElement.GetProperty("error").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("traceId").GetString()));
    }

    private static HttpRequestMessage AuthenticatedGet(string path, string userId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Test-UserId", userId);
        return request;
    }

    private static WeakTopicDto CreateTopic(int id) => new(
        id,
        $"Topic {id}",
        0.5m,
        "medium",
        0.8m,
        0.4m,
        DateTime.UtcNow.AddMinutes(-id),
        id * 2);

    private static WeakSubtopicDto CreateSubtopic(int id) => new(
        id,
        $"Subtopic {id}",
        id,
        $"Topic {id}",
        0.5m,
        "medium",
        0.8m,
        0.4m,
        DateTime.UtcNow.AddMinutes(-id),
        id * 2);
}

public sealed class AnalyticsContractWebApplicationFactory : CustomWebApplicationFactory<Program>
{
    public RecordingWeaknessAnalysisService Service { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IWeaknessAnalysisService>();
            services.AddSingleton<IWeaknessAnalysisService>(Service);
        });
    }
}

public sealed class RecordingWeaknessAnalysisService : IWeaknessAnalysisService
{
    private int topicCalls;
    private int subtopicCalls;
    private int recommendationCalls;

    public int TotalCalls => Volatile.Read(ref topicCalls) + Volatile.Read(ref subtopicCalls) + Volatile.Read(ref recommendationCalls);
    public Guid LastUserId { get; private set; }
    public int LastTake { get; private set; }
    public int LastTopicTake { get; private set; }
    public int LastSubtopicTake { get; private set; }
    public int LastRecommendationTake { get; private set; }
    public CancellationToken LastCancellationToken { get; private set; }
    public IReadOnlyList<WeakTopicDto> Topics { get; set; } = Array.Empty<WeakTopicDto>();
    public IReadOnlyList<WeakSubtopicDto> Subtopics { get; set; } = Array.Empty<WeakSubtopicDto>();
    public IReadOnlyList<PracticeRecommendationDto> Recommendations { get; set; } = Array.Empty<PracticeRecommendationDto>();
    public Exception? ExceptionToThrow { get; set; }

    public void Reset()
    {
        Interlocked.Exchange(ref topicCalls, 0);
        Interlocked.Exchange(ref subtopicCalls, 0);
        Interlocked.Exchange(ref recommendationCalls, 0);
        LastUserId = Guid.Empty;
        LastTake = 0;
        LastTopicTake = 0;
        LastSubtopicTake = 0;
        LastRecommendationTake = 0;
        LastCancellationToken = default;
        Topics = Array.Empty<WeakTopicDto>();
        Subtopics = Array.Empty<WeakSubtopicDto>();
        Recommendations = Array.Empty<PracticeRecommendationDto>();
        ExceptionToThrow = null;
    }

    public Task AnalyzeUserAsync(Guid userId, CancellationToken ct) => Task.CompletedTask;

    public Task<IReadOnlyList<WeakTopicDto>> GetWeakTopicsAsync(
        Guid userId,
        int take = 5,
        CancellationToken ct = default)
    {
        Interlocked.Increment(ref topicCalls);
        Record(userId, take, ct);
        LastTopicTake = take;
        ThrowIfConfigured();
        return Task.FromResult<IReadOnlyList<WeakTopicDto>>(Topics.Take(take).ToList());
    }

    public Task<IReadOnlyList<WeakSubtopicDto>> GetWeakSubtopicsAsync(
        Guid userId,
        int take = 10,
        CancellationToken ct = default)
    {
        Interlocked.Increment(ref subtopicCalls);
        Record(userId, take, ct);
        LastSubtopicTake = take;
        ThrowIfConfigured();
        return Task.FromResult<IReadOnlyList<WeakSubtopicDto>>(Subtopics.Take(take).ToList());
    }

    public Task<IReadOnlyList<PracticeRecommendationDto>> GeneratePracticeRecommendationsAsync(
        Guid userId,
        int take = 10,
        CancellationToken ct = default)
    {
        Interlocked.Increment(ref recommendationCalls);
        Record(userId, take, ct);
        LastRecommendationTake = take;
        ThrowIfConfigured();
        return Task.FromResult<IReadOnlyList<PracticeRecommendationDto>>(Recommendations.Take(take).ToList());
    }

    private void Record(Guid userId, int take, CancellationToken cancellationToken)
    {
        LastUserId = userId;
        LastTake = take;
        LastCancellationToken = cancellationToken;
    }

    private void ThrowIfConfigured()
    {
        if (ExceptionToThrow is not null)
            throw ExceptionToThrow;
    }
}
