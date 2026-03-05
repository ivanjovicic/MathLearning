using MathLearning.Api.Services;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Endpoints;

public class AdaptiveApiFacadeIntegrationTests
{
    [Fact]
    public async Task GetAdaptiveRecommendations_WhenOriginBecomesUnavailable_ReturnsCachedPayload()
    {
        var fakeAdaptiveService = new FakeAdaptiveLearningService();
        var facade = BuildFacade(fakeAdaptiveService);

        var warmup = await facade.GetAdaptiveRecommendationsAsync("1", CancellationToken.None);
        Assert.True(warmup.Success);
        Assert.NotNull(warmup.Data);
        Assert.False(warmup.Data!.ServedFromCache);

        fakeAdaptiveService.FailRecommendations = true;
        var offline = await facade.GetAdaptiveRecommendationsAsync("1", CancellationToken.None);

        Assert.True(offline.Success);
        Assert.NotNull(offline.Data);
        Assert.True(offline.Data!.ServedFromCache);
        Assert.NotEmpty(offline.Data.Payload.Recommendations);
    }

    [Fact]
    public async Task GetAdaptivePath_WhenOriginBecomesUnavailable_ReturnsCachedPayload()
    {
        var fakeAdaptiveService = new FakeAdaptiveLearningService();
        var facade = BuildFacade(fakeAdaptiveService);

        var warmup = await facade.GetAdaptivePathAsync("1", CancellationToken.None);
        Assert.True(warmup.Success);
        Assert.NotNull(warmup.Data);
        Assert.False(warmup.Data!.ServedFromCache);

        fakeAdaptiveService.FailRecommendations = true;
        fakeAdaptiveService.FailReviews = true;

        var offline = await facade.GetAdaptivePathAsync("1", CancellationToken.None);
        Assert.True(offline.Success);
        Assert.NotNull(offline.Data);
        Assert.True(offline.Data!.ServedFromCache);
        Assert.NotNull(offline.Data.Payload);
    }

    private static AdaptiveApiFacade BuildFacade(FakeAdaptiveLearningService fakeAdaptiveService)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache(options => options.SizeLimit = 100);
        services.AddSingleton<InMemoryCacheService>();
        services.AddSingleton<IAdaptiveAnalyticsService, AdaptiveAnalyticsService>();
        services.AddSingleton<IAdaptiveLearningService>(fakeAdaptiveService);
        services.AddScoped<AdaptiveApiFacade>();

        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AdaptiveApiFacade>();
    }

    private sealed class FakeAdaptiveLearningService : IAdaptiveLearningService
    {
        public bool FailRecommendations { get; set; }
        public bool FailReviews { get; set; }

        public Task<AdaptiveSession> GeneratePracticeSessionAsync(string userId)
        {
            var session = new AdaptiveSession
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                Items =
                [
                    new AdaptiveSessionItem
                    {
                        Id = Guid.NewGuid(),
                        QuestionId = 10,
                        TopicId = 1,
                        SubtopicId = 1,
                        SourceType = "weak",
                        DifficultyLevel = AdaptiveDifficultyLevels.Medium,
                        Sequence = 1
                    }
                ]
            };

            return Task.FromResult(session);
        }

        public Task<AdaptiveAnswerResult> SubmitAnswerAsync(string userId, AdaptiveAnswerRequest request) =>
            Task.FromResult(new AdaptiveAnswerResult
            {
                IsCorrect = true,
                DifficultyLevel = AdaptiveDifficultyLevels.Medium,
                NextReviewAt = DateTime.UtcNow.AddDays(1),
                ReviewIntervalDays = 1,
                ReviewEasinessFactor = 2.5
            });

        public Task<List<AdaptiveRecommendation>> GetRecommendationsAsync(string userId)
        {
            if (FailRecommendations)
                throw new TimeoutException("offline");

            return Task.FromResult(new List<AdaptiveRecommendation>
            {
                new()
                {
                    TopicId = 1,
                    Topic = "Algebra",
                    Difficulty = AdaptiveDifficultyLevels.Medium,
                    QuestionCount = 5,
                    Confidence = 0.7,
                    Reason = "practice"
                }
            });
        }

        public Task<List<ReviewItem>> GetDueReviewsAsync(string userId)
        {
            if (FailReviews)
                throw new TimeoutException("offline");

            return Task.FromResult(new List<ReviewItem>
            {
                new()
                {
                    QuestionId = 10,
                    TopicId = 1,
                    Topic = "Algebra",
                    DueAt = DateTime.UtcNow,
                    IntervalDays = 1,
                    RepetitionCount = 0,
                    EasinessFactor = 2.5,
                    Difficulty = AdaptiveDifficultyLevels.Medium,
                    Overdue = false
                }
            });
        }

        public Task DetectWeakTopicsAsync(string userId) => Task.CompletedTask;
    }
}
