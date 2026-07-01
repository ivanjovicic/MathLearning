using System.Net;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Endpoints;

public sealed class SrsEndpointsIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;

    public SrsEndpointsIntegrationTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Daily_DueOnly_ReturnsDueQuestionsInReviewOrder()
    {
        var userId = NewUserId("daily-due-only");
        await SeedQuestionStatsAsync(userId,
            (1, DateTime.UtcNow.AddDays(-1), 2.0),
            (2, DateTime.UtcNow.AddDays(-3), 1.1));

        var response = await SendGetAsync($"/api/quiz/srs/daily?limit=2", userId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ids = await ReadQuestionIdsAsync(response);
        Assert.Equal(new[] { 2, 1 }, ids);
    }

    [Fact]
    public async Task Daily_DuePlusRandom_PadsWithoutDuplicateQuestionIds()
    {
        var userId = NewUserId("daily-padding");
        await SeedQuestionStatsAsync(userId,
            (1, DateTime.UtcNow.AddDays(-1), 1.2),
            (2, DateTime.UtcNow.AddDays(-2), 1.3));

        var response = await SendGetAsync($"/api/quiz/srs/daily?limit=5", userId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ids = await ReadQuestionIdsAsync(response);
        Assert.Equal(5, ids.Length);
        Assert.Equal(5, ids.Distinct().Count());
        Assert.Contains(1, ids);
        Assert.Contains(2, ids);
        Assert.All(ids.Except(new[] { 1, 2 }), id => Assert.InRange(id, 3, 20));
    }

    [Fact]
    public async Task Daily_NoDueQuestions_ReturnsEmptyList()
    {
        var userId = NewUserId("daily-empty");

        var response = await SendGetAsync($"/api/quiz/srs/daily?limit=5", userId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ids = await ReadQuestionIdsAsync(response);
        Assert.Empty(ids);
    }

    [Fact]
    public async Task Daily_RespectsLimit_WhenMoreQuestionsAreDueThanRequested()
    {
        var userId = NewUserId("daily-limit");
        await SeedQuestionStatsAsync(userId,
            (1, DateTime.UtcNow.AddDays(-1), 1.0),
            (2, DateTime.UtcNow.AddDays(-2), 1.0),
            (3, DateTime.UtcNow.AddDays(-3), 1.0),
            (4, DateTime.UtcNow.AddDays(-4), 1.0),
            (5, DateTime.UtcNow.AddDays(-5), 1.0));

        var response = await SendGetAsync($"/api/quiz/srs/daily?limit=3", userId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ids = await ReadQuestionIdsAsync(response);
        Assert.Equal(new[] { 5, 4, 3 }, ids);
    }

    [Fact]
    public async Task Mixed_DueAndRandom_AreDisjointAndRespectCount()
    {
        var userId = NewUserId("mixed-padding");
        await SeedQuestionStatsAsync(userId,
            (1, DateTime.UtcNow.AddDays(-1), 1.2),
            (2, DateTime.UtcNow.AddDays(-2), 1.3));

        var response = await SendGetAsync($"/api/quiz/srs/mixed?count=5", userId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var root = await ReadJsonAsync(response);
        var srsIds = ReadQuestionIds(root.GetProperty("srs"));
        var randomIds = ReadQuestionIds(root.GetProperty("random"));

        Assert.Equal(new[] { 2, 1 }, srsIds);
        Assert.Equal(3, randomIds.Length);
        Assert.Equal(3, randomIds.Distinct().Count());
        Assert.Empty(srsIds.Intersect(randomIds));
    }

    private async Task<HttpResponseMessage> SendGetAsync(string path, string userId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation("X-Test-UserId", userId);
        return await _client.SendAsync(request);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private static async Task<int[]> ReadQuestionIdsAsync(HttpResponseMessage response)
        => ReadQuestionIds(await ReadJsonAsync(response));

    private static int[] ReadQuestionIds(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Expected a JSON array.");

        return element.EnumerateArray()
            .Select(item => item.GetProperty("id").GetInt32())
            .ToArray();
    }

    private async Task SeedQuestionStatsAsync(
        string userId,
        params (int QuestionId, DateTime NextReview, double Ease)[] stats)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        foreach (var stat in stats)
        {
            db.QuestionStats.Add(new QuestionStat
            {
                UserId = userId,
                QuestionId = stat.QuestionId,
                NextReview = stat.NextReview,
                Ease = stat.Ease,
                SuccessStreak = 1
            });
        }

        await db.SaveChangesAsync();
    }

    private static string NewUserId(string suffix)
        => $"srs-{suffix}-{Guid.NewGuid():N}";
}
