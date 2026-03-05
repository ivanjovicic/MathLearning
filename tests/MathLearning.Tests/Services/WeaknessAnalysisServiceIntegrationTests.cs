using MathLearning.Api.Services;
using MathLearning.Application.Helpers;
using MathLearning.Application.Services;
using MathLearning.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace MathLearning.Tests.Services;

public class WeaknessAnalysisServiceIntegrationTests
{
    [Fact]
    public async Task AnalyzeUserAsync_ZeroAttempts_ReturnsEmptyWeakness()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var service = new WeaknessAnalysisService(db, NullLogger<WeaknessAnalysisService>.Instance);
        var userId = UserIdGuidMapper.FromIdentityUserId("1");

        await service.AnalyzeUserAsync(userId, CancellationToken.None);

        var topics = await service.GetWeakTopicsAsync(userId, 5, CancellationToken.None);
        var subtopics = await service.GetWeakSubtopicsAsync(userId, 5, CancellationToken.None);
        var recommendations = await service.GeneratePracticeRecommendationsAsync(userId, 5, CancellationToken.None);

        Assert.Empty(topics);
        Assert.Empty(subtopics);
        Assert.Empty(recommendations);
    }

    [Fact]
    public async Task AnalyzeUserAsync_OneIncorrectAttempt_ProducesHighWeakness()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var scheduler = new FakeWeaknessScheduler();
        var ingest = new QuizAttemptIngestService(db, scheduler, NullLogger<QuizAttemptIngestService>.Instance);
        var service = new WeaknessAnalysisService(db, NullLogger<WeaknessAnalysisService>.Instance);
        var now = DateTime.UtcNow;

        await ingest.IngestAttemptsAsync(
            userId: "1",
            attempts:
            [
                new QuizAttemptIngestItem(
                    QuizId: Guid.NewGuid(),
                    QuestionId: 1,
                    SubtopicId: 1,
                    Correct: false,
                    TimeSpentMs: 90_000,
                    CreatedAtUtc: now)
            ],
            ct: CancellationToken.None);

        var userId = scheduler.LastEnqueued!.Value;
        await service.AnalyzeUserAsync(userId, CancellationToken.None);

        var topics = await service.GetWeakTopicsAsync(userId, 3, CancellationToken.None);
        var recommendations = await service.GeneratePracticeRecommendationsAsync(userId, 3, CancellationToken.None);

        Assert.NotEmpty(topics);
        Assert.Equal("high", topics[0].WeaknessLevel);
        Assert.True(topics[0].Accuracy < 0.60m);
        Assert.NotEmpty(recommendations);
    }

    [Fact]
    public async Task AnalyzeUserAsync_ThousandAttemptsWithHighAccuracy_ProducesLowWeakness()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var scheduler = new FakeWeaknessScheduler();
        var ingest = new QuizAttemptIngestService(db, scheduler, NullLogger<QuizAttemptIngestService>.Instance);
        var service = new WeaknessAnalysisService(db, NullLogger<WeaknessAnalysisService>.Instance);
        var attempts = new List<QuizAttemptIngestItem>(1000);
        var quizId = Guid.NewGuid();
        var start = DateTime.UtcNow.AddDays(-4);

        for (var i = 0; i < 1000; i++)
        {
            var questionId = (i % 20) + 1;
            var correct = i < 900; // 90% accuracy
            attempts.Add(new QuizAttemptIngestItem(
                QuizId: quizId,
                QuestionId: questionId,
                SubtopicId: 1,
                Correct: correct,
                TimeSpentMs: 5_000 + (i % 500),
                CreatedAtUtc: start.AddMinutes(i)));
        }

        await ingest.IngestAttemptsAsync("1", attempts, CancellationToken.None);
        var userId = scheduler.LastEnqueued!.Value;
        await service.AnalyzeUserAsync(userId, CancellationToken.None);

        var topics = await service.GetWeakTopicsAsync(userId, 1, CancellationToken.None);
        Assert.Single(topics);
        Assert.True(topics[0].Accuracy >= 0.80m);
        Assert.Equal("low", topics[0].WeaknessLevel);
    }

    private sealed class FakeWeaknessScheduler : IWeaknessAnalysisScheduler
    {
        public Guid? LastEnqueued { get; private set; }

        public void Enqueue(Guid userId)
        {
            LastEnqueued = userId;
        }
    }
}
