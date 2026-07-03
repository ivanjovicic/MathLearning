using System.Collections.Concurrent;
using MathLearning.Api.Services;
using MathLearning.Application.Helpers;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace MathLearning.Tests.Services;

public sealed class QuizAttemptIngestServiceRelationalTests
{
    [Fact]
    public async Task EmptyBatch_PerformsNoWritesAndDoesNotScheduleAnalysis()
    {
        await using var database = await IngestTestDatabase.CreateAsync();
        await using var db = database.CreateContext();
        var scheduler = new RecordingWeaknessAnalysisScheduler();
        var service = CreateService(db, scheduler);

        await service.IngestAttemptsAsync("empty-user", Array.Empty<QuizAttemptIngestItem>());

        await using var verification = database.CreateContext();
        Assert.False(await verification.QuizAttempts.AnyAsync());
        Assert.False(await verification.UserTopicStats.AnyAsync());
        Assert.False(await verification.UserSubtopicStats.AnyAsync());
        Assert.Empty(scheduler.EnqueuedUserIds);
    }

    [Fact]
    public async Task NewBatch_PersistsAttemptsNormalizesTimeAndBuildsTopicAndSubtopicStats()
    {
        await using var database = await IngestTestDatabase.CreateAsync();
        var (topicId, subtopicId) = await database.SeedTopicAsync();
        await using var db = database.CreateContext();
        var scheduler = new RecordingWeaknessAnalysisScheduler();
        var service = CreateService(db, scheduler);
        var userId = $"ingest-user-{Guid.NewGuid():N}";
        var mappedUserId = UserIdGuidMapper.FromIdentityUserId(userId);
        var quizId = Guid.NewGuid();
        var firstAt = DateTime.UtcNow.AddMinutes(-3);
        var secondAt = firstAt.AddMinutes(1);
        var lastAt = secondAt.AddMinutes(1);

        await service.IngestAttemptsAsync(
            userId,
            new[]
            {
                new QuizAttemptIngestItem(quizId, 101, subtopicId, true, -50, firstAt),
                new QuizAttemptIngestItem(quizId, 102, subtopicId, false, 1_500, secondAt),
                new QuizAttemptIngestItem(quizId, 103, subtopicId, true, 2_500, lastAt)
            });

        await using var verification = database.CreateContext();
        var attempts = await verification.QuizAttempts
            .Where(x => x.UserId == mappedUserId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();
        Assert.Equal(3, attempts.Count);
        Assert.Equal(0, attempts[0].TimeSpentMs);
        Assert.Equal(1_500, attempts[1].TimeSpentMs);
        Assert.Equal(2_500, attempts[2].TimeSpentMs);
        Assert.All(attempts, x => Assert.Equal(quizId, x.QuizId));
        Assert.All(attempts, x => Assert.Equal(topicId, x.TopicId));
        Assert.All(attempts, x => Assert.Equal(subtopicId, x.SubtopicId));

        var topicStat = await verification.UserTopicStats.SingleAsync(x =>
            x.UserId == mappedUserId && x.TopicId == topicId);
        Assert.Equal(3, topicStat.TotalQuestions);
        Assert.Equal(2, topicStat.CorrectAnswers);
        Assert.Equal(0.6667m, topicStat.Accuracy);
        Assert.Equal(lastAt, topicStat.LastAttempt);
        Assert.InRange(topicStat.WeaknessScore, 0m, 3m);

        var subtopicStat = await verification.UserSubtopicStats.SingleAsync(x =>
            x.UserId == mappedUserId && x.SubtopicId == subtopicId);
        Assert.Equal(3, subtopicStat.TotalQuestions);
        Assert.Equal(2, subtopicStat.CorrectAnswers);
        Assert.Equal(0.6667m, subtopicStat.Accuracy);
        Assert.Equal(lastAt, subtopicStat.LastAttempt);
        Assert.InRange(subtopicStat.WeaknessScore, 0m, 3m);

        Assert.Equal(new[] { mappedUserId }, scheduler.EnqueuedUserIds);
    }

    [Fact]
    public async Task ExistingStats_AreAccumulatedAndNeverMoveLastAttemptBackwards()
    {
        await using var database = await IngestTestDatabase.CreateAsync();
        var (topicId, subtopicId) = await database.SeedTopicAsync();
        var userId = $"existing-stats-{Guid.NewGuid():N}";
        var mappedUserId = UserIdGuidMapper.FromIdentityUserId(userId);
        var existingLastAttempt = DateTime.UtcNow.AddMinutes(-5);

        await using (var seed = database.CreateContext())
        {
            seed.UserTopicStats.Add(new UserTopicStat
            {
                UserId = mappedUserId,
                TopicId = topicId,
                TotalQuestions = 2,
                CorrectAnswers = 1,
                Accuracy = 0.5m,
                LastAttempt = existingLastAttempt,
                WeaknessScore = 0.1m
            });
            seed.UserSubtopicStats.Add(new UserSubtopicStat
            {
                UserId = mappedUserId,
                SubtopicId = subtopicId,
                TotalQuestions = 2,
                CorrectAnswers = 1,
                Accuracy = 0.5m,
                LastAttempt = existingLastAttempt,
                WeaknessScore = 0.1m
            });
            await seed.SaveChangesAsync();
        }

        await using var db = database.CreateContext();
        var scheduler = new RecordingWeaknessAnalysisScheduler();
        var service = CreateService(db, scheduler);
        var olderAttempt = existingLastAttempt.AddMinutes(-10);

        await service.IngestAttemptsAsync(
            userId,
            new[]
            {
                new QuizAttemptIngestItem(Guid.NewGuid(), 201, subtopicId, true, 800, olderAttempt)
            });

        await using var verification = database.CreateContext();
        var topicStat = await verification.UserTopicStats.SingleAsync(x =>
            x.UserId == mappedUserId && x.TopicId == topicId);
        Assert.Equal(3, topicStat.TotalQuestions);
        Assert.Equal(2, topicStat.CorrectAnswers);
        Assert.Equal(0.6667m, topicStat.Accuracy);
        Assert.Equal(existingLastAttempt, topicStat.LastAttempt);

        var subtopicStat = await verification.UserSubtopicStats.SingleAsync(x =>
            x.UserId == mappedUserId && x.SubtopicId == subtopicId);
        Assert.Equal(3, subtopicStat.TotalQuestions);
        Assert.Equal(2, subtopicStat.CorrectAnswers);
        Assert.Equal(0.6667m, subtopicStat.Accuracy);
        Assert.Equal(existingLastAttempt, subtopicStat.LastAttempt);
        Assert.Equal(new[] { mappedUserId }, scheduler.EnqueuedUserIds);
    }

    [Fact]
    public async Task FailureAfterSqlWasIssued_RollsBackAttemptsAndStatsAndDoesNotScheduleAnalysis()
    {
        var failureInterceptor = new ThrowAfterIngestSaveInterceptor();
        await using var database = await IngestTestDatabase.CreateAsync(failureInterceptor);
        var (_, subtopicId) = await database.SeedTopicAsync();
        await using var db = database.CreateContext();
        var scheduler = new RecordingWeaknessAnalysisScheduler();
        var service = CreateService(db, scheduler);
        var userId = $"rollback-ingest-{Guid.NewGuid():N}";
        failureInterceptor.Arm();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.IngestAttemptsAsync(
                userId,
                new[]
                {
                    new QuizAttemptIngestItem(
                        Guid.NewGuid(),
                        301,
                        subtopicId,
                        true,
                        900,
                        DateTime.UtcNow.AddMinutes(-1))
                }));

        Assert.Equal(ThrowAfterIngestSaveInterceptor.SecretMessage, error.Message);
        Assert.Equal(1, failureInterceptor.ThrowCount);

        await using var verification = database.CreateContext();
        Assert.False(await verification.QuizAttempts.AnyAsync());
        Assert.False(await verification.UserTopicStats.AnyAsync());
        Assert.False(await verification.UserSubtopicStats.AnyAsync());
        Assert.Empty(scheduler.EnqueuedUserIds);
    }

    [Fact]
    public async Task CancelledBatch_PersistsNothingAndDoesNotScheduleAnalysis()
    {
        await using var database = await IngestTestDatabase.CreateAsync();
        var (_, subtopicId) = await database.SeedTopicAsync();
        await using var db = database.CreateContext();
        var scheduler = new RecordingWeaknessAnalysisScheduler();
        var service = CreateService(db, scheduler);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.IngestAttemptsAsync(
                "cancelled-ingest-user",
                new[]
                {
                    new QuizAttemptIngestItem(
                        Guid.NewGuid(),
                        401,
                        subtopicId,
                        true,
                        1_000,
                        DateTime.UtcNow)
                },
                cancellation.Token));

        await using var verification = database.CreateContext();
        Assert.False(await verification.QuizAttempts.AnyAsync());
        Assert.False(await verification.UserTopicStats.AnyAsync());
        Assert.False(await verification.UserSubtopicStats.AnyAsync());
        Assert.Empty(scheduler.EnqueuedUserIds);
    }

    private static QuizAttemptIngestService CreateService(
        ApiDbContext db,
        IWeaknessAnalysisScheduler scheduler) =>
        new(db, scheduler, NullLogger<QuizAttemptIngestService>.Instance);

    private sealed class RecordingWeaknessAnalysisScheduler : IWeaknessAnalysisScheduler
    {
        private readonly ConcurrentQueue<Guid> userIds = new();

        public IReadOnlyList<Guid> EnqueuedUserIds => userIds.ToArray();

        public void Enqueue(Guid userId) => userIds.Enqueue(userId);
    }

    private sealed class ThrowAfterIngestSaveInterceptor : SaveChangesInterceptor
    {
        public const string SecretMessage = "SECRET_QUIZ_ATTEMPT_INGEST_AFTER_SAVE_FAILURE";

        private readonly ConcurrentDictionary<Guid, bool> matchingSaves = new();
        private int armed;
        private int throwCount;

        public int ThrowCount => Volatile.Read(ref throwCount);

        public void Arm()
        {
            matchingSaves.Clear();
            Interlocked.Exchange(ref throwCount, 0);
            Interlocked.Exchange(ref armed, 1);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var context = eventData.Context;
            if (Volatile.Read(ref armed) == 1 &&
                context is not null &&
                context.ChangeTracker.Entries<QuizAttempt>().Any(x => x.State == EntityState.Added))
            {
                matchingSaves[context.ContextId.InstanceId] = true;
            }

            return ValueTask.FromResult(result);
        }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            var context = eventData.Context;
            if (context is not null &&
                matchingSaves.TryRemove(context.ContextId.InstanceId, out _) &&
                Interlocked.CompareExchange(ref throwCount, 1, 0) == 0)
            {
                throw new InvalidOperationException(SecretMessage);
            }

            return ValueTask.FromResult(result);
        }
    }

    private sealed class IngestTestDatabase : IAsyncDisposable
    {
        private readonly string path;
        private readonly DbContextOptions<ApiDbContext> options;

        private IngestTestDatabase(string path, DbContextOptions<ApiDbContext> options)
        {
            this.path = path;
            this.options = options;
        }

        public static async Task<IngestTestDatabase> CreateAsync(params IInterceptor[] interceptors)
        {
            var path = Path.Combine(Path.GetTempPath(), $"mathlearning-ingest-{Guid.NewGuid():N}.db");
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared,
                Pooling = false,
                DefaultTimeout = 30
            }.ToString();

            var builder = new DbContextOptionsBuilder<ApiDbContext>()
                .UseSqlite(connectionString);
            if (interceptors.Length > 0)
                builder.AddInterceptors(interceptors);

            var database = new IngestTestDatabase(path, builder.Options);
            await using var setup = database.CreateContext();
            await setup.Database.EnsureCreatedAsync();
            await setup.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=30000;");
            return database;
        }

        public ApiDbContext CreateContext() => new(options);

        public async Task<(int TopicId, int SubtopicId)> SeedTopicAsync()
        {
            await using var db = CreateContext();
            var topic = new Topic("Ingest topic", "Relational ingest tests");
            db.Topics.Add(topic);
            await db.SaveChangesAsync();

            var subtopic = new Subtopic("Ingest subtopic", topic.Id);
            db.Subtopics.Add(subtopic);
            await db.SaveChangesAsync();
            return (topic.Id, subtopic.Id);
        }

        public ValueTask DisposeAsync()
        {
            DeleteIfExists(path);
            DeleteIfExists($"{path}-wal");
            DeleteIfExists($"{path}-shm");
            return ValueTask.CompletedTask;
        }

        private static void DeleteIfExists(string file)
        {
            if (File.Exists(file))
                File.Delete(file);
        }
    }
}
