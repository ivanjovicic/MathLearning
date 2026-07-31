using System.Text.Json;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using MathLearning.Api.Services;
using MathLearning.Application.DTOs.Practice;
using MathLearning.Application.Helpers;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services.Idempotency;
using MathLearning.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace MathLearning.Tests.Idempotency;

public sealed class PracticeSessionIdempotencyTests
{
    [Fact]
    public async Task SubmitAnswerAsync_DuplicateReplay_ReturnsSettledSnapshot_AndSkipsDuplicateWrites()
    {
        await using var database = await SqliteFileTestDatabase.CreateAsync();
        var scheduler = new FakeWeaknessScheduler();
        var backgroundJobs = new RecordingBackgroundJobClient();

        Guid sessionId;
        int questionId;
        SubmitPracticeAnswerResponse first;
        SubmitPracticeAnswerResponse replay;

        await using (var db = database.CreateContext())
        {
            var sut = BuildService(db, scheduler, backgroundJobs);
            var start = await sut.StartSessionAsync(
                "1",
                new StartPracticeSessionRequest(
                    UserId: null,
                    SkillNodeId: "fractions_basics",
                    TopicId: 1,
                    SubtopicId: 1,
                    TargetQuestions: 2,
                    PreferredDifficulty: "medium"),
                CancellationToken.None);

            Assert.NotNull(start.Question);

            sessionId = start.SessionId;
            questionId = start.Question!.Id;

            var correctOption = await GetCorrectOptionIdAsync(db, questionId);
            first = await sut.SubmitAnswerAsync(
                "1",
                sessionId,
                new SubmitPracticeAnswerRequest(questionId, correctOption.ToString(), 12000),
                CancellationToken.None);
        }

        await using (var replayDb = database.CreateContext())
        {
            var sut = BuildService(replayDb, scheduler, backgroundJobs);
            var correctOption = await GetCorrectOptionIdAsync(replayDb, questionId);
            replay = await sut.SubmitAnswerAsync(
                "1",
                sessionId,
                new SubmitPracticeAnswerRequest(questionId, correctOption.ToString(), 12000),
                CancellationToken.None);
        }

        Assert.Equal(
            IdempotencyPayloadCanonicalizer.CanonicalizeToJson(first),
            IdempotencyPayloadCanonicalizer.CanonicalizeToJson(replay));

        await using var verification = database.CreateContext();
        Assert.Equal(1, await verification.QuizAttempts.CountAsync());
        Assert.Equal(2, await verification.PracticeSessionItems.CountAsync(x => x.SessionId == sessionId));

        var settledItem = await verification.PracticeSessionItems.SingleAsync(x =>
            x.SessionId == sessionId && x.QuestionId == questionId);
        Assert.False(string.IsNullOrWhiteSpace(settledItem.SubmissionFingerprintJson));
        Assert.False(string.IsNullOrWhiteSpace(settledItem.SettledResponseJson));
    }

    [Fact]
    public async Task SubmitAnswerAsync_SameItemDifferentPayload_ReturnsConflict()
    {
        await using var database = await SqliteFileTestDatabase.CreateAsync();
        var scheduler = new FakeWeaknessScheduler();
        var backgroundJobs = new RecordingBackgroundJobClient();

        Guid sessionId;
        int questionId;

        await using (var db = database.CreateContext())
        {
            var sut = BuildService(db, scheduler, backgroundJobs);
            var start = await sut.StartSessionAsync(
                "1",
                new StartPracticeSessionRequest(
                    UserId: null,
                    SkillNodeId: "fractions_basics",
                    TopicId: 1,
                    SubtopicId: 1,
                    TargetQuestions: 2,
                    PreferredDifficulty: "medium"),
                CancellationToken.None);

            Assert.NotNull(start.Question);
            sessionId = start.SessionId;
            questionId = start.Question!.Id;

            var correctOption = await GetCorrectOptionIdAsync(db, questionId);
            await sut.SubmitAnswerAsync(
                "1",
                sessionId,
                new SubmitPracticeAnswerRequest(questionId, correctOption.ToString(), 12000),
                CancellationToken.None);
        }

        await using var replayDb = database.CreateContext();
        var replaySut = BuildService(replayDb, scheduler, backgroundJobs);
        var wrongOption = await GetWrongOptionIdAsync(replayDb, questionId);

        var conflict = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            replaySut.SubmitAnswerAsync(
                "1",
                sessionId,
                new SubmitPracticeAnswerRequest(questionId, wrongOption.ToString(), 12000),
                CancellationToken.None));

        Assert.Contains("different payload", conflict.Message, StringComparison.OrdinalIgnoreCase);

        await using var verification = database.CreateContext();
        Assert.Equal(1, await verification.QuizAttempts.CountAsync());
        Assert.Equal(2, await verification.PracticeSessionItems.CountAsync(x => x.SessionId == sessionId));
    }

    [Fact]
    public async Task CompleteSessionAsync_ConcurrentDuplicates_SettleOnce_AndEnqueueJobsOnce()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await database.MigrateApiAsync();
        await database.SeedApiAsync();

        var scheduler = new FakeWeaknessScheduler();
        var backgroundJobs = new RecordingBackgroundJobClient();

        Guid sessionId;
        int questionId;

        await using (var db = new ApiDbContext(database.CreateApiOptions()))
        {
            var sut = BuildService(db, scheduler, backgroundJobs);
            var start = await sut.StartSessionAsync(
                "1",
                new StartPracticeSessionRequest(
                    UserId: null,
                    SkillNodeId: "fractions_basics",
                    TopicId: 1,
                    SubtopicId: 1,
                    TargetQuestions: 1,
                    PreferredDifficulty: "medium"),
                CancellationToken.None);

            Assert.NotNull(start.Question);

            sessionId = start.SessionId;
            questionId = start.Question!.Id;

            var correctOption = await GetCorrectOptionIdAsync(db, questionId);
            var answer = await sut.SubmitAnswerAsync(
                "1",
                sessionId,
                new SubmitPracticeAnswerRequest(questionId, correctOption.ToString(), 12000),
                CancellationToken.None);

            Assert.True(answer.IsCorrect);
        }

        await using var firstDb = new ApiDbContext(database.CreateApiOptions());
        await using var secondDb = new ApiDbContext(database.CreateApiOptions());
        var firstSut = BuildService(firstDb, scheduler, backgroundJobs);
        var secondSut = BuildService(secondDb, scheduler, backgroundJobs);

        var firstTask = firstSut.CompleteSessionAsync("1", sessionId, CancellationToken.None);
        var secondTask = secondSut.CompleteSessionAsync("1", sessionId, CancellationToken.None);
        var results = await Task.WhenAll(firstTask, secondTask);

        Assert.Equal(
            IdempotencyPayloadCanonicalizer.CanonicalizeToJson(results[0]),
            IdempotencyPayloadCanonicalizer.CanonicalizeToJson(results[1]));
        Assert.Equal(3, backgroundJobs.EnqueueCount);

        await using var verification = new ApiDbContext(database.CreateApiOptions());
        Assert.Equal(1, await verification.UserDailyStats.CountAsync(x => x.UserId == "1"));

        var storedSession = await verification.PracticeSessions.SingleAsync(x => x.Id == sessionId);
        Assert.Equal("Completed", storedSession.Status);
        Assert.False(string.IsNullOrWhiteSpace(storedSession.CompletionResponseJson));
    }

    [Fact]
    public async Task SubmitAnswerAsync_ConcurrentIdenticalSubmissions_SettleOnce()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await database.MigrateApiAsync();
        await database.SeedApiAsync();

        var scheduler = new FakeWeaknessScheduler();
        var backgroundJobs = new RecordingBackgroundJobClient();

        Guid sessionId;
        int questionId;
        string correctOption;

        await using (var db = new ApiDbContext(database.CreateApiOptions()))
        {
            var sut = BuildService(db, scheduler, backgroundJobs);
            var start = await sut.StartSessionAsync(
                "1",
                new StartPracticeSessionRequest(
                    UserId: null,
                    SkillNodeId: "fractions_basics",
                    TopicId: 1,
                    SubtopicId: 1,
                    TargetQuestions: 2,
                    PreferredDifficulty: "medium"),
                CancellationToken.None);

            Assert.NotNull(start.Question);

            sessionId = start.SessionId;
            questionId = start.Question!.Id;
            correctOption = (await GetCorrectOptionIdAsync(db, questionId)).ToString();
        }

        var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, 20)
            .Select(async _ =>
            {
                await startGate.Task;
                await using var db = new ApiDbContext(database.CreateApiOptions());
                var sut = BuildService(db, scheduler, backgroundJobs);
                return await sut.SubmitAnswerAsync(
                    "1",
                    sessionId,
                    new SubmitPracticeAnswerRequest(questionId, correctOption, 12000),
                    CancellationToken.None);
            })
            .ToArray();

        startGate.SetResult();
        var results = await Task.WhenAll(tasks);

        var settledResponse = JsonSerializer.Serialize(results.First(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.All(results, result =>
        {
            Assert.Equal(settledResponse, JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        });

        await using var verification = new ApiDbContext(database.CreateApiOptions());
        Assert.Equal(1, await verification.QuizAttempts.CountAsync(x => x.QuizId == sessionId));
        Assert.Equal(2, await verification.PracticeSessionItems.CountAsync(x => x.SessionId == sessionId));

        var settledItem = await verification.PracticeSessionItems.SingleAsync(x =>
            x.SessionId == sessionId && x.QuestionId == questionId);
        Assert.False(string.IsNullOrWhiteSpace(settledItem.SubmissionFingerprintJson));
        Assert.False(string.IsNullOrWhiteSpace(settledItem.SettledResponseJson));
    }

    [Fact]
    public async Task SubmitAnswerAsync_CancellationBeforeCommit_RollsBackAllMutations()
    {
        await using var database = await SqliteFileTestDatabase.CreateAsync();
        var scheduler = new FakeWeaknessScheduler();
        var backgroundJobs = new RecordingBackgroundJobClient();
        using var cancellation = new CancellationTokenSource();
        var interceptor = new CancelAfterSaveInterceptor(cancellation);

        Guid sessionId;
        int questionId;

        await using (var db = database.CreateContext(interceptor))
        {
            var sut = BuildService(db, scheduler, backgroundJobs);
            var start = await sut.StartSessionAsync(
                "1",
                new StartPracticeSessionRequest(
                    UserId: null,
                    SkillNodeId: "fractions_basics",
                    TopicId: 1,
                    SubtopicId: 1,
                    TargetQuestions: 2,
                    PreferredDifficulty: "medium"),
                CancellationToken.None);

            Assert.NotNull(start.Question);

            sessionId = start.SessionId;
            questionId = start.Question!.Id;

            var correctOption = await GetCorrectOptionIdAsync(db, questionId);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                sut.SubmitAnswerAsync(
                    "1",
                    sessionId,
                    new SubmitPracticeAnswerRequest(questionId, correctOption.ToString(), 9000),
                    cancellation.Token));
        }

        await using var verification = database.CreateContext();
        Assert.Equal(0, await verification.QuizAttempts.CountAsync());

        var storedItem = await verification.PracticeSessionItems.SingleAsync(x =>
            x.SessionId == sessionId && x.QuestionId == questionId);
        Assert.Null(storedItem.AnsweredAt);
        Assert.Null(storedItem.SubmissionFingerprintJson);
        Assert.Null(storedItem.SettledResponseJson);
    }

    private static PracticeSessionService BuildService(
        ApiDbContext db,
        IWeaknessAnalysisScheduler scheduler,
        RecordingBackgroundJobClient backgroundJobs)
    {
        var bkt = new BktService(new MemoryCache(new MemoryCacheOptions()));
        var selector = new DeterministicQuestionSelector(db);
        var analyticsUpdater = new PracticeAnalyticsUpdater(
            db,
            scheduler,
            NullLogger<PracticeAnalyticsUpdater>.Instance);
        var adaptiveAnalytics = new AdaptiveAnalyticsService(NullLogger<AdaptiveAnalyticsService>.Instance);
        var practiceJobs = new PracticeBackgroundJobs(
            backgroundJobs,
            analyticsUpdater,
            adaptiveAnalytics,
            NullLogger<PracticeBackgroundJobs>.Instance);

        return new PracticeSessionService(
            db,
            selector,
            bkt,
            analyticsUpdater,
            practiceJobs,
            adaptiveAnalytics,
            new NoOpAnswerPatternAntiCheatService(),
            NullLogger<PracticeSessionService>.Instance);
    }

    private sealed class DeterministicQuestionSelector : IQuestionSelector
    {
        private readonly ApiDbContext _db;

        public DeterministicQuestionSelector(ApiDbContext db)
        {
            _db = db;
        }

        public async Task<SelectedQuestion?> GetNextQuestionAsync(
            QuestionSelectionCriteria criteria,
            CancellationToken ct = default)
        {
            var excluded = criteria.ExcludedQuestionIds?.Distinct().ToHashSet() ?? [];
            var normalizedDifficulty = PracticeDifficulties.Normalize(criteria.Difficulty);
            var targetDifficulty = normalizedDifficulty switch
            {
                PracticeDifficulties.Easy => 2,
                PracticeDifficulties.Medium => 3,
                PracticeDifficulties.Hard => 4,
                _ => 3
            };

            var candidates = await (
                from question in _db.Questions.AsNoTracking().Include(x => x.Options)
                join subtopic in _db.Subtopics.AsNoTracking()
                    on question.SubtopicId equals subtopic.Id
                where (!criteria.SubtopicId.HasValue || question.SubtopicId == criteria.SubtopicId.Value)
                    && (!criteria.TopicId.HasValue || subtopic.TopicId == criteria.TopicId.Value)
                    && !excluded.Contains(question.Id)
                select new
                {
                    Question = question,
                    TopicId = subtopic.TopicId,
                    Distance = Math.Abs(question.Difficulty - targetDifficulty)
                })
                .ToListAsync(ct);

            var candidate = candidates
                .OrderBy(x => x.Distance)
                .ThenBy(x => x.Question.Id)
                .FirstOrDefault();

            if (candidate is null)
                return null;

            return new SelectedQuestion(
                candidate.Question.Id,
                candidate.Question.Text,
                candidate.Question.Options
                    .OrderBy(x => x.Id)
                    .Select(x => new SelectedQuestionOption(
                        x.Id,
                        x.Text,
                        x.IsCorrect,
                        x.TextFormat,
                        x.RenderMode,
                        TranslationHelper.ResolveSemanticsAltText(x.SemanticsAltText, x.Text, x.TextFormat)))
                    .ToList(),
                candidate.TopicId,
                candidate.Question.SubtopicId,
                candidate.Question.Difficulty switch
                {
                    <= 2 => PracticeDifficulties.Easy,
                    3 => PracticeDifficulties.Medium,
                    _ => PracticeDifficulties.Hard
                },
                candidate.Question.CorrectAnswer,
                candidate.Question.TextFormat,
                candidate.Question.TextRenderMode,
                TranslationHelper.ResolveSemanticsAltText(
                    candidate.Question.SemanticsAltText,
                    candidate.Question.Text,
                    candidate.Question.TextFormat));
        }
    }

    private static async Task<int> GetCorrectOptionIdAsync(ApiDbContext db, int questionId)
    {
        var question = await db.Questions
            .Include(x => x.Options)
            .FirstAsync(x => x.Id == questionId);
        return question.Options.First(x => x.IsCorrect).Id;
    }

    private static async Task<int> GetWrongOptionIdAsync(ApiDbContext db, int questionId)
    {
        var question = await db.Questions
            .Include(x => x.Options)
            .FirstAsync(x => x.Id == questionId);
        return question.Options.First(x => !x.IsCorrect).Id;
    }

    private sealed class FakeWeaknessScheduler : IWeaknessAnalysisScheduler
    {
        public Guid? LastEnqueued { get; private set; }

        public bool Enqueue(Guid userId)
        {
            LastEnqueued = userId;
            return true;
        }
    }

    private sealed class RecordingBackgroundJobClient : IBackgroundJobClient
    {
        private int _enqueueCount;

        public int EnqueueCount => _enqueueCount;

        public string Create(Job job, IState state)
        {
            Interlocked.Increment(ref _enqueueCount);
            return Guid.NewGuid().ToString("N");
        }

        public bool ChangeState(string jobId, IState state, string expectedState)
        {
            return true;
        }
    }

    private sealed class CancelAfterSaveInterceptor : SaveChangesInterceptor
    {
        private readonly CancellationTokenSource _cancellation;
        private int _saveCount;

        public CancelAfterSaveInterceptor(CancellationTokenSource cancellation)
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

    private sealed class SqliteFileTestDatabase : IAsyncDisposable
    {
        private readonly string _filePath;
        private readonly string _connectionString;

        private SqliteFileTestDatabase(string filePath, string connectionString)
        {
            _filePath = filePath;
            _connectionString = connectionString;
        }

        public static async Task<SqliteFileTestDatabase> CreateAsync()
        {
            var filePath = Path.Combine(Path.GetTempPath(), $"mathlearning-practice-{Guid.NewGuid():N}.db");
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = filePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared,
                Pooling = false,
                DefaultTimeout = 30
            }.ToString();

            var database = new SqliteFileTestDatabase(filePath, connectionString);
            await using var setup = database.CreateContext();
            await setup.Database.EnsureCreatedAsync();
            await setup.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
            await setup.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=30000;");
            await TestDbContextFactory.SeedAsync(setup);
            return database;
        }

        public ApiDbContext CreateContext(params IInterceptor[] interceptors)
        {
            var builder = new DbContextOptionsBuilder<ApiDbContext>()
                .UseSqlite(_connectionString);

            if (interceptors.Length > 0)
                builder.AddInterceptors(interceptors);

            return new ApiDbContext(builder.Options);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (File.Exists(_filePath))
                    File.Delete(_filePath);
            }
            catch
            {
            }

            await Task.CompletedTask;
        }
    }
}
