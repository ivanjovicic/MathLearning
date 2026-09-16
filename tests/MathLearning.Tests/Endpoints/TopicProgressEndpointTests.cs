using System.Net;
using System.Net.Http.Json;
using MathLearning.Api;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Endpoints;

public sealed class TopicProgressEndpointTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public TopicProgressEndpointTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task TopicProgress_IncludesPlayableCountsAndCanStartQuiz()
    {
        await SeedPublishedTopicAsync("progress-topic", subtopicQuestionCount: 3);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/progress/topics");
        request.Headers.Add("X-Test-UserId", "1");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<TopicProgressResponse[]>();
        Assert.NotNull(payload);

        var topic = Assert.Single(payload!, x => x.Name == "progress-topic");
        Assert.Equal(3, topic.PlayableQuestionCount);
        Assert.Equal(topic.Unlocked && topic.PlayableQuestionCount > 0, topic.CanStartQuiz);
    }

    [Fact]
    public async Task SubtopicProgress_ReturnsNumericSubtopicIdsForQuizStart()
    {
        var seeded = await SeedPublishedTopicAsync("progress-subtopics", subtopicQuestionCount: 2);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/progress/topics/{seeded.TopicId}/subtopics");
        request.Headers.Add("X-Test-UserId", "1");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<SubtopicProgressResponse[]>();
        Assert.NotNull(payload);

        var subtopic = Assert.Single(payload!);
        Assert.Equal(seeded.SubtopicId, subtopic.SubtopicId);
        Assert.Equal(2, subtopic.PlayableQuestionCount);

        using var topicRequest = new HttpRequestMessage(HttpMethod.Get, "/api/progress/topics");
        topicRequest.Headers.Add("X-Test-UserId", "1");
        var topicResponse = await _client.SendAsync(topicRequest);
        var topics = await topicResponse.Content.ReadFromJsonAsync<TopicProgressResponse[]>();
        var topic = Assert.Single(topics!, x => x.TopicId == seeded.TopicId);
        Assert.Equal(topic.Unlocked && subtopic.PlayableQuestionCount > 0, subtopic.CanStartQuiz);
    }

    private async Task<(int TopicId, int SubtopicId)> SeedPublishedTopicAsync(
        string scope,
        int subtopicQuestionCount)
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

        for (var i = 1; i <= subtopicQuestionCount; i++)
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
        return (topic.Id, subtopic.Id);
    }

    private sealed record TopicProgressResponse(
        int TopicId,
        string Name,
        double Accuracy,
        bool Unlocked,
        int PlayableQuestionCount,
        bool CanStartQuiz);

    private sealed record SubtopicProgressResponse(
        int SubtopicId,
        int TopicId,
        string Name,
        int PlayableQuestionCount,
        bool CanStartQuiz);
}
