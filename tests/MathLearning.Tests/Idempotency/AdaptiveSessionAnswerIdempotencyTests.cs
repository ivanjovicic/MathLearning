using MathLearning.Api.Services;
using MathLearning.Application.DTOs.AntiCheat;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace MathLearning.Tests.Idempotency;

public sealed class AdaptiveSessionAnswerIdempotencyTests
{
    [Fact]
    public async Task SubmitAnswerAsync_DuplicateReplay_ReturnsSettledSnapshot_AndSkipsDownstreamWrites()
    {
        if (!IsValidationRequired())
            return;

        await using var database = await PostgresTestDatabase.CreateAsync();
        await database.MigrateApiAsync();
        await database.SeedApiAsync();
        var scenario = await SeedScenarioAsync(database);

        AdaptiveAnswerSubmissionResult first;
        await using (var db = CreateContext(database))
        {
            var sut = CreateSut(db);
            first = await sut.SubmitAnswerAsync("1", BuildRequest(scenario), CancellationToken.None);
        }

        AdaptiveAnswerSubmissionResult replay;
        await using (var db = CreateContext(database))
        {
            var sut = CreateSut(db);
            replay = await sut.SubmitAnswerAsync("1", BuildRequest(scenario), CancellationToken.None);
        }

        Assert.False(first.WasReplayed);
        Assert.True(replay.WasReplayed);
        Assert.Equal(first.Result.IsCorrect, replay.Result.IsCorrect);
        Assert.Equal(first.Result.DifficultyLevel, replay.Result.DifficultyLevel);
        Assert.Equal(first.Result.TopicId, replay.Result.TopicId);
        Assert.Equal(first.Result.TopicMasteryScore, replay.Result.TopicMasteryScore);
        Assert.Equal(first.Result.IsWeakTopic, replay.Result.IsWeakTopic);
        Assert.Equal(first.Result.NextReviewAt, replay.Result.NextReviewAt);
        Assert.Equal(first.Result.ReviewIntervalDays, replay.Result.ReviewIntervalDays);
        Assert.Equal(first.Result.ReviewEasinessFactor, replay.Result.ReviewEasinessFactor);
        Assert.Equal(first.Result.Explanation, replay.Result.Explanation);

        await using var verification = CreateContext(database);
        Assert.Equal(1, await verification.UserQuestionHistories.CountAsync(x => x.AdaptiveSessionItemId == scenario.ItemId));
        Assert.Equal(1, await verification.Outbox.CountAsync(x =>
            x.Type.Contains("AdaptiveAnswerLegacySrsSyncRequested")));
        var history = await verification.UserQuestionHistories.SingleAsync(x => x.AdaptiveSessionItemId == scenario.ItemId);
        Assert.NotNull(history.RequestFingerprintJson);
        Assert.NotNull(history.SettledResponseJson);
    }

    [Fact]
    public async Task SubmitAnswerAsync_SameItemDifferentPayload_ReturnsConflict()
    {
        if (!IsValidationRequired())
            return;

        await using var database = await PostgresTestDatabase.CreateAsync();
        await database.MigrateApiAsync();
        await database.SeedApiAsync();
        var scenario = await SeedScenarioAsync(database);

        await using (var db = CreateContext(database))
        {
            var sut = CreateSut(db);
            var first = await sut.SubmitAnswerAsync("1", BuildRequest(scenario), CancellationToken.None);
            Assert.False(first.WasReplayed);
        }

        await using var replayDb = CreateContext(database);
        var replaySut = CreateSut(replayDb);
        var conflict = await Assert.ThrowsAsync<AdaptiveAnswerConflictException>(() =>
            replaySut.SubmitAnswerAsync(
                "1",
                BuildRequest(scenario, answer: "999"),
                CancellationToken.None));

        Assert.Contains("different payload", conflict.Message, StringComparison.OrdinalIgnoreCase);

        await using var verification = CreateContext(database);
        Assert.Equal(1, await verification.UserQuestionHistories.CountAsync());
        Assert.Equal(1, await verification.Outbox.CountAsync(x =>
            x.Type.Contains("AdaptiveAnswerLegacySrsSyncRequested")));
    }

    [Fact]
    public async Task SubmitAnswerAsync_CancellationAfterFirstSave_RollsBackAllAdaptiveMutations()
    {
        if (!IsValidationRequired())
            return;

        await using var database = await PostgresTestDatabase.CreateAsync();
        await database.MigrateApiAsync();
        await database.SeedApiAsync();

        await using var seedDb = CreateContext(database);

        var question = await seedDb.Questions
            .Include(x => x.Subtopic)
            .Include(x => x.Options)
            .FirstAsync();

        var session = CreateAdaptiveSession(question);
        session.Items[0].AdaptiveSessionId = session.Id;
        seedDb.AdaptiveSessions.Add(session);
        await seedDb.SaveChangesAsync();

        using var cancellation = new CancellationTokenSource();
        var interceptor = new CancelAfterFirstSaveInterceptor(cancellation);

        await using var db = CreateContext(database, interceptor);
        var sut = CreateSut(db);

        var request = BuildRequest(
            new AdaptiveAnswerScenario(
                session.Id,
                session.Items[0].Id,
                question.Id,
                question.Subtopic!.TopicId,
                question.SubtopicId,
                question.Options.Single(o => o.IsCorrect).Text));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            sut.SubmitAnswerAsync("1", request, cancellation.Token));

        await using var verification = CreateContext(database);
        Assert.Equal(0, await verification.UserQuestionHistories.CountAsync());
        Assert.Equal(0, await verification.ReviewSchedules.CountAsync());
        Assert.Equal(0, await verification.UserTopicMasteries.CountAsync());
        Assert.Equal(0, await verification.UserLearningProfiles.CountAsync());
        Assert.Equal(0, await verification.Outbox.CountAsync(x =>
            x.Type.Contains("AdaptiveAnswerLegacySrsSyncRequested")));

        var storedSessionItem = await verification.AdaptiveSessionItems.SingleAsync(x => x.Id == session.Items[0].Id);
        Assert.Null(storedSessionItem.IsCorrect);
        Assert.Null(storedSessionItem.AnsweredAt);
    }

    private static AdaptiveAnswerRequest BuildRequest(AdaptiveAnswerScenario scenario, string? answer = null) =>
        new()
        {
            AdaptiveSessionId = scenario.SessionId,
            AdaptiveSessionItemId = scenario.ItemId,
            QuestionId = scenario.QuestionId,
            Answer = answer ?? scenario.CorrectAnswer,
            ResponseTimeSeconds = 12,
            Confidence = 0.75d
        };

    private static AdaptiveSession CreateAdaptiveSession(Question question)
    {
        if (question.Subtopic is null)
            throw new InvalidOperationException("Seeded adaptive question is missing subtopic metadata.");

        return new AdaptiveSession
        {
            Id = Guid.NewGuid(),
            UserId = "1",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(35),
            ProfileDifficulty = AdaptiveDifficultyLevels.Medium,
            Items =
            [
                new AdaptiveSessionItem
                {
                    Id = Guid.NewGuid(),
                    AdaptiveSessionId = Guid.Empty,
                    QuestionId = question.Id,
                    TopicId = question.Subtopic.TopicId,
                    SubtopicId = question.SubtopicId,
                    SourceType = "adaptive",
                    DifficultyLevel = AdaptiveDifficultyLevels.Medium,
                    Sequence = 1,
                    CreatedAt = DateTime.UtcNow
                }
            ]
        };
    }

    private static AdaptiveLearningService CreateSut(ApiDbContext db)
    {
        var cache = new InMemoryCacheService(new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 }));
        return new AdaptiveLearningService(
            db,
            cache,
            new NoOpAnswerPatternAntiCheatService(),
            NullLogger<AdaptiveLearningService>.Instance);
    }

    private static bool IsValidationRequired()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable("POSTGRES_PROVIDER_TESTS_REQUIRED"),
            "1",
            StringComparison.Ordinal);
    }

    private static ApiDbContext CreateContext(PostgresTestDatabase database, params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<ApiDbContext>()
            .UseNpgsql(database.DatabaseConnectionString);

        if (interceptors.Length > 0)
            options.AddInterceptors(interceptors);

        return new ApiDbContext(options.Options);
    }

    private static async Task<AdaptiveAnswerScenario> SeedScenarioAsync(PostgresTestDatabase database)
    {
        await using var db = CreateContext(database);

        var question = await db.Questions
            .Include(x => x.Subtopic)
            .Include(x => x.Options)
            .FirstAsync();

        var session = CreateAdaptiveSession(question);
        session.Items[0].AdaptiveSessionId = session.Id;
        db.AdaptiveSessions.Add(session);
        await db.SaveChangesAsync();

        return new AdaptiveAnswerScenario(
            session.Id,
            session.Items[0].Id,
            question.Id,
            question.Subtopic!.TopicId,
            question.SubtopicId,
            question.Options.Single(o => o.IsCorrect).Text);
    }

    private sealed class CancelAfterFirstSaveInterceptor : SaveChangesInterceptor
    {
        private readonly CancellationTokenSource _cancellation;
        private int _saveCount;

        public CancelAfterFirstSaveInterceptor(CancellationTokenSource cancellation)
        {
            _cancellation = cancellation;
        }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _saveCount) == 1)
                _cancellation.Cancel();

            return base.SavedChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed record AdaptiveAnswerScenario(
        Guid SessionId,
        Guid ItemId,
        int QuestionId,
        int TopicId,
        int SubtopicId,
        string CorrectAnswer);

}
