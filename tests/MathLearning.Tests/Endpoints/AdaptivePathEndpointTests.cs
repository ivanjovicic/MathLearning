using System.Net;
using System.Net.Http.Json;
using MathLearning.Api;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Endpoints;

public sealed class AdaptivePathEndpointTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AdaptivePathEndpointTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AdaptivePath_NewUser_IncludesStarterRecommendationsWithSubtopicId()
    {
        await SeedPublishedTopicAsync("adaptive-path-starter", questionCount: 2);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/adaptive/path");
        request.Headers.Add("X-Test-UserId", "1");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<AdaptivePathApiResult>();
        Assert.NotNull(payload);
        Assert.True(payload!.Success);
        Assert.NotNull(payload.Data);
        Assert.NotEmpty(payload.Data!.Payload.Recommendations);

        var recommendation = Assert.Single(
            payload.Data.Payload.Recommendations,
            item => item.Topic == "adaptive-path-starter" && item.SubtopicId > 0);
        Assert.True(recommendation.TopicId > 0);
        Assert.True(recommendation.QuestionCount > 0);
    }

    private async Task SeedPublishedTopicAsync(string scope, int questionCount)
    {
        using var scopeHandle = _factory.Services.CreateScope();
        var db = scopeHandle.ServiceProvider.GetRequiredService<ApiDbContext>();

        var category = new Category($"{scope}-category");
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var topic = new Topic($"{scope}", $"{scope} description");
        db.Topics.Add(topic);
        await db.SaveChangesAsync();

        var subtopic = new Subtopic($"{scope}-subtopic", topic.Id);
        db.Subtopics.Add(subtopic);
        await db.SaveChangesAsync();

        for (var i = 1; i <= questionCount; i++)
        {
            var question = new Question($"{scope} question {i}?", 1, category.Id);
            question.SetSubtopic(subtopic.Id);
            question.ReplaceOptions(new[]
            {
                new QuestionOption($"A{i}", true, order: 1),
                new QuestionOption($"B{i}", false, order: 2)
            });
            question.SetPublishState(QuestionPublishStates.Published, "test-fixture", DateTime.UtcNow);
            db.Questions.Add(question);
        }

        await db.SaveChangesAsync();
    }

    private sealed record AdaptivePathApiResult(
        bool Success,
        AdaptivePathData? Data);

    private sealed record AdaptivePathData(
        AdaptivePathPayload Payload);

    private sealed record AdaptivePathPayload(
        IReadOnlyList<AdaptiveRecommendationResponse> Recommendations,
        IReadOnlyList<object> DueReviews,
        DateTime GeneratedAtUtc);

    private sealed record AdaptiveRecommendationResponse(
        int TopicId,
        int? SubtopicId,
        string Topic,
        int QuestionCount);
}
