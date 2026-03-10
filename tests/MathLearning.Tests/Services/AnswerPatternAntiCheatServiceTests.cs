using MathLearning.Application.DTOs.AntiCheat;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Services.AntiCheat;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MathLearning.Tests.Services;

public sealed class AnswerPatternAntiCheatServiceTests
{
    [Fact]
    public async Task EvaluateAndTrackAsync_DoesNotFlagNormalHumanPattern()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync();
        var baseTime = DateTime.UtcNow.AddMinutes(-12);

        for (var i = 0; i < 6; i++)
        {
            db.UserAnswers.Add(new MathLearning.Domain.Entities.UserAnswer
            {
                UserId = "1",
                QuestionId = i + 1,
                QuizSessionId = Guid.NewGuid(),
                Answer = (i + 2).ToString(),
                IsCorrect = i % 2 == 0,
                TimeSpentSeconds = 8 + i,
                AnsweredAt = baseTime.AddMinutes(i * 2)
            });
        }

        await db.SaveChangesAsync();

        var sut = CreateService(db);
        var result = await sut.EvaluateAndTrackAsync(
            new AntiCheatAnswerObservationInput(
                "1",
                "quiz_answer",
                8,
                null,
                1,
                Guid.NewGuid(),
                null,
                null,
                "42",
                true,
                9000,
                null,
                DateTime.UtcNow),
            CancellationToken.None);

        Assert.False(result.IsSuspicious);
        Assert.Empty(db.AnswerPatternDetectionLogs.Local);
    }

    [Fact]
    public async Task EvaluateAndTrackBatchAsync_FlagsRapidPerfectBurst()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync();
        var baseTime = DateTime.UtcNow.AddMinutes(-2);
        var inputs = Enumerable.Range(0, 8)
            .Select(i => new AntiCheatAnswerObservationInput(
                "1",
                "quiz_offline_submit",
                i + 1,
                null,
                1,
                Guid.NewGuid(),
                "device-rapid",
                i + 1,
                $@"x+{i}",
                true,
                1200,
                null,
                baseTime.AddSeconds(i * 5)))
            .ToList();

        var sut = CreateService(db);
        var results = await sut.EvaluateAndTrackBatchAsync(inputs, CancellationToken.None);
        await db.SaveChangesAsync();

        Assert.Contains(results, x => x.IsSuspicious);
        Assert.NotEmpty(await db.AnswerPatternDetectionLogs.ToListAsync());
        Assert.All(
            await db.AnswerPatternDetectionLogs.ToListAsync(),
            x => Assert.True(x.RiskScore >= 50));
    }

    [Fact]
    public async Task ProcessPendingReviewsAsync_CompletesMlReviewForQueuedDetections()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync();
        var baseTime = DateTime.UtcNow.AddMinutes(-2);
        var inputs = Enumerable.Range(0, 8)
            .Select(i => new AntiCheatAnswerObservationInput(
                "1",
                "quiz_offline_submit",
                i + 10,
                null,
                1,
                Guid.NewGuid(),
                "device-ml",
                i + 1,
                $@"x+{i}",
                true,
                1200,
                0.99d,
                baseTime.AddSeconds(i * 5)))
            .ToList();

        var sut = CreateService(db);
        await sut.EvaluateAndTrackBatchAsync(inputs, CancellationToken.None);
        await db.SaveChangesAsync();

        var detection = await db.AnswerPatternDetectionLogs.OrderByDescending(x => x.DetectedAtUtc).FirstAsync();
        Assert.Equal(AntiCheatMlReviewStatuses.Queued, detection.MlReviewStatus);

        var processed = await sut.ProcessPendingReviewsAsync(10, CancellationToken.None);
        await db.Entry(detection).ReloadAsync();

        Assert.True(processed >= 1);
        Assert.Equal(AntiCheatMlReviewStatuses.Completed, detection.MlReviewStatus);
        Assert.NotNull(detection.MlReviewOutputJson);
        Assert.NotNull(detection.MlModelName);
    }

    private static AnswerPatternAntiCheatService CreateService(MathLearning.Infrastructure.Persistance.ApiDbContext db)
        => new(
            db,
            Options.Create(new AntiCheatOptions
            {
                HistoryLookbackMinutes = 30,
                MaxHistoryEvents = 40,
                RapidCorrectBurstThreshold = 6,
                FastResponseThresholdMs = 2200,
                DetectionThreshold = 50
            }),
            new AntiCheatMlPromptBuilder(),
            NullLogger<AnswerPatternAntiCheatService>.Instance);
}
