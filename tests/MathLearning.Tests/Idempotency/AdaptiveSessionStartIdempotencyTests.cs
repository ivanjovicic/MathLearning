using System.Net;
using System.Text;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Api.Services;
using MathLearning.Application.DTOs.Adaptive;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services;
using MathLearning.Infrastructure.Services.Idempotency;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace MathLearning.Tests.Idempotency;

public sealed class AdaptiveSessionStartIdempotencyTests
{
    [Fact]
    public async Task KeyedStart_ReplaysExactSessionSnapshot_AndPersistsSingleLedgerRow()
    {
        await using var database = await SqliteFileTestDatabase.CreateAsync();
        await using var db = database.CreateContext();
        await database.SeedAdaptiveCatalogAsync(db);

        var facade = BuildFacade(new PersistingAdaptiveLearningService(db));
        var ledgerService = CreateLedgerService(db);
        const string userId = "adaptive-start-user-1";
        const string operationId = "adaptive-start-op-1";
        const string idempotencyKey = "adaptive-start-key-1";
        var requestPayload = new
        {
            topicId = 101,
            topic = "adaptive_topic",
            operationId,
            idempotencyKey
        };

        var first = await ExecuteAsync(
            await facade.StartAdaptiveSessionAsync(
                userId,
                db,
                ledgerService,
                requestPayload,
                operationId,
                idempotencyKey,
                CancellationToken.None));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstJson = JsonDocument.Parse(first.Body);
        var firstSessionId = firstJson.RootElement.GetProperty("adaptiveSessionId").GetGuid();
        var firstItemId = firstJson.RootElement.GetProperty("items").EnumerateArray().Single().GetProperty("adaptiveSessionItemId").GetGuid();

        var replay = await ExecuteAsync(
            await facade.StartAdaptiveSessionAsync(
                userId,
                db,
                ledgerService,
                requestPayload,
                operationId,
                idempotencyKey,
                CancellationToken.None));

        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(first.Body, replay.Body);

        using var scope = database.CreateVerificationScope();
        var verificationDb = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        Assert.Equal(1, await verificationDb.AdaptiveSessions.CountAsync(x => x.UserId == userId));
        Assert.Equal(1, await verificationDb.AdaptiveSessionItems.CountAsync());
        Assert.Equal(1, await verificationDb.IdempotencyLedgers.CountAsync(x =>
            x.UserId == userId &&
            x.OperationType == "adaptive_session_start"));

        var replaySessionId = JsonDocument.Parse(replay.Body).RootElement.GetProperty("adaptiveSessionId").GetGuid();
        var replayItemId = JsonDocument.Parse(replay.Body).RootElement.GetProperty("items").EnumerateArray().Single().GetProperty("adaptiveSessionItemId").GetGuid();
        Assert.Equal(firstSessionId, replaySessionId);
        Assert.Equal(firstItemId, replayItemId);
    }

    [Fact]
    public async Task SameKeysDifferentNormalizedPayload_ReturnsIdempotencyConflict()
    {
        await using var database = await SqliteFileTestDatabase.CreateAsync();
        await using var db = database.CreateContext();
        await database.SeedAdaptiveCatalogAsync(db);

        var facade = BuildFacade(new PersistingAdaptiveLearningService(db));
        var ledgerService = CreateLedgerService(db);
        const string userId = "adaptive-start-user-2";
        const string operationId = "adaptive-start-op-2";
        const string idempotencyKey = "adaptive-start-key-2";

        var firstPayload = new
        {
            topicId = 101,
            topic = "adaptive_topic",
            operationId,
            idempotencyKey
        };

        var secondPayload = new
        {
            topicId = 202,
            topic = "adaptive_topic",
            operationId,
            idempotencyKey
        };

        var first = await ExecuteAsync(
            await facade.StartAdaptiveSessionAsync(
                userId,
                db,
                ledgerService,
                firstPayload,
                operationId,
                idempotencyKey,
                CancellationToken.None));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var conflict = await ExecuteAsync(
            await facade.StartAdaptiveSessionAsync(
                userId,
                db,
                ledgerService,
                secondPayload,
                operationId,
                idempotencyKey,
                CancellationToken.None));

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using var conflictJson = JsonDocument.Parse(conflict.Body);
        Assert.Equal("idempotency_conflict", conflictJson.RootElement.GetProperty("errorCode").GetString());

        using var scope = database.CreateVerificationScope();
        var verificationDb = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        Assert.Equal(1, await verificationDb.AdaptiveSessions.CountAsync(x => x.UserId == userId));
        Assert.Equal(1, await verificationDb.IdempotencyLedgers.CountAsync(x =>
            x.UserId == userId &&
            x.OperationType == "adaptive_session_start"));
    }

    [Fact]
    public async Task SameKeysDifferentUsers_AreIsolated()
    {
        await using var database = await SqliteFileTestDatabase.CreateAsync();
        await using var db = database.CreateContext();
        await database.SeedAdaptiveCatalogAsync(db);

        var facade = BuildFacade(new PersistingAdaptiveLearningService(db));
        var ledgerService = CreateLedgerService(db);
        const string operationId = "adaptive-start-op-3";
        const string idempotencyKey = "adaptive-start-key-3";
        var requestPayload = new
        {
            topicId = 101,
            topic = "adaptive_topic",
            operationId,
            idempotencyKey
        };

        await ExecuteAsync(
            await facade.StartAdaptiveSessionAsync(
                "adaptive-user-a",
                db,
                ledgerService,
                requestPayload,
                operationId,
                idempotencyKey,
                CancellationToken.None));

        await ExecuteAsync(
            await facade.StartAdaptiveSessionAsync(
                "adaptive-user-b",
                db,
                ledgerService,
                requestPayload,
                operationId,
                idempotencyKey,
                CancellationToken.None));

        using var scope = database.CreateVerificationScope();
        var verificationDb = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        Assert.Equal(2, await verificationDb.AdaptiveSessions.CountAsync());
        Assert.Equal(2, await verificationDb.IdempotencyLedgers.CountAsync(x =>
            x.OperationId == operationId &&
            x.OperationType == "adaptive_session_start"));
    }

    [Fact]
    public async Task LegacyNoKeyPath_IsExplicitlyNonRetryable()
    {
        await using var database = await SqliteFileTestDatabase.CreateAsync();
        await using var db = database.CreateContext();
        await database.SeedAdaptiveCatalogAsync(db);

        var facade = BuildFacade(new PersistingAdaptiveLearningService(db));
        var ledgerService = CreateLedgerService(db);
        var legacyPayload = new
        {
            topicId = 101,
            topic = "adaptive_topic"
        };

        var first = await ExecuteAsync(
            await facade.StartAdaptiveSessionAsync(
                "adaptive-legacy-user",
                db,
                ledgerService,
                legacyPayload,
                operationId: null,
                idempotencyKey: null,
                CancellationToken.None));
        var replay = await ExecuteAsync(
            await facade.StartAdaptiveSessionAsync(
                "adaptive-legacy-user",
                db,
                ledgerService,
                legacyPayload,
                operationId: null,
                idempotencyKey: null,
                CancellationToken.None));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.NotEqual(first.Body, replay.Body);

        using var scope = database.CreateVerificationScope();
        var verificationDb = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        Assert.Equal(2, await verificationDb.AdaptiveSessions.CountAsync(x => x.UserId == "adaptive-legacy-user"));
        Assert.False(await verificationDb.IdempotencyLedgers.AnyAsync(x =>
            x.UserId == "adaptive-legacy-user" &&
            x.OperationType == "adaptive_session_start"));
    }

    [Fact]
    public async Task ConcurrentSameKeyStarts_SettleOnce_UnderSqlite()
    {
        await using var database = await SqliteFileTestDatabase.CreateAsync();
        await using var seedDb = database.CreateContext();
        await database.SeedAdaptiveCatalogAsync(seedDb);
        var coordinator = new OrderedInsertCoordinator();
        const string userId = "adaptive-race-user";
        const string operationId = "adaptive-race-op";
        const string idempotencyKey = "adaptive-race-key";
        var requestPayload = new
        {
            topicId = 101,
            topic = "adaptive_topic",
            operationId,
            idempotencyKey
        };

        var firstTask = BeginAndPersistAdaptiveSessionAsync(
            database,
            coordinator,
            participant: 1,
            userId,
            operationId,
            idempotencyKey,
            requestPayload);
        var secondTask = BeginAndPersistAdaptiveSessionAsync(
            database,
            coordinator,
            participant: 2,
            userId,
            operationId,
            idempotencyKey,
            requestPayload);

        var results = await Task.WhenAll(firstTask, secondTask);
        Assert.Equal(1, results.Count(x => x.ShouldProcess));
        Assert.Equal(1, results.Count(x => x.IsExisting));
        Assert.Single(results.Select(x => x.LedgerId).Distinct());

        using var scope = database.CreateVerificationScope();
        var verificationDb = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        Assert.Equal(1, await verificationDb.AdaptiveSessions.CountAsync(x => x.UserId == userId));
        Assert.Equal(1, await verificationDb.AdaptiveSessionItems.CountAsync());
        Assert.Equal(1, await verificationDb.IdempotencyLedgers.CountAsync(x =>
            x.UserId == userId &&
            x.OperationType == "adaptive_session_start" &&
            x.Status == IdempotencyLedgerStatuses.Completed));
    }

    private static AdaptiveApiFacade BuildFacade(PersistingAdaptiveLearningService adaptiveLearningService)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache(options => options.SizeLimit = 100);
        services.AddSingleton<InMemoryCacheService>();
        services.AddSingleton<IAdaptiveAnalyticsService, AdaptiveAnalyticsService>();
        services.AddSingleton<IAdaptiveLearningService>(adaptiveLearningService);
        services.AddScoped<AdaptiveApiFacade>();

        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AdaptiveApiFacade>();
    }

    private static IdempotencyLedgerService CreateLedgerService(ApiDbContext db) =>
        new(
            db,
            NullLogger<IdempotencyLedgerService>.Instance,
            new IdempotencyObservabilityService(NullLogger<IdempotencyObservabilityService>.Instance));

    private static async Task<IdempotencyLedgerBeginResult> BeginAndPersistAdaptiveSessionAsync(
        SqliteFileTestDatabase database,
        OrderedInsertCoordinator coordinator,
        int participant,
        string userId,
        string operationId,
        string idempotencyKey,
        object requestPayload)
    {
        var interceptor = new CoordinatedAddedEntityInterceptor<IdempotencyLedger>(coordinator, participant);
        await using var db = database.CreateContext(interceptor);
        var ledgerService = CreateLedgerService(db);

        try
        {
            var begin = await ledgerService.BeginOrGetExistingAsync(
                userId,
                "adaptive_session_start",
                operationId,
                idempotencyKey,
                "POST /api/adaptive/session/start",
                requestPayload,
                CancellationToken.None);

            if (begin.ShouldProcess)
            {
                var session = await CreateAndPersistAdaptiveSessionAsync(db, userId);
                var sessionDto = MapSession(session);

                await ledgerService.CompleteAsync(
                    begin.LedgerId,
                    sessionDto,
                    StatusCodes.Status200OK,
                    CancellationToken.None);
            }

            return begin;
        }
        finally
        {
            if (participant == 1)
                coordinator.ReleaseSecondWriter();
        }
    }

    private static async Task<AdaptiveSession> CreateAndPersistAdaptiveSessionAsync(ApiDbContext db, string userId)
    {
        var question = await db.Questions
            .Include(x => x.Subtopic)
            .SingleAsync();

        if (question.Subtopic is null)
            throw new InvalidOperationException("Seeded adaptive question is missing subtopic metadata.");

        var session = new AdaptiveSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(35),
            ProfileDifficulty = AdaptiveDifficultyLevels.Medium,
            Items =
            [
                new AdaptiveSessionItem
                {
                    Id = Guid.NewGuid(),
                    AdaptiveSessionId = Guid.NewGuid(),
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

        session.Items[0].AdaptiveSessionId = session.Id;
        db.AdaptiveSessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    private static AdaptiveSessionDto MapSession(AdaptiveSession session)
    {
        return new AdaptiveSessionDto(
            session.Id,
            session.CreatedAt,
            session.ExpiresAt,
            session.ProfileDifficulty,
            session.Items
                .OrderBy(i => i.Sequence)
                .Select(i => new AdaptiveSessionItemDto(
                    i.Id,
                    i.QuestionId,
                    i.TopicId,
                    i.SubtopicId,
                    i.SourceType,
                    i.DifficultyLevel,
                    i.Sequence))
                .ToList());
    }

    private static async Task<(HttpStatusCode StatusCode, string Body)> ExecuteAsync(IResult result)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.RequestServices = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

        await result.ExecuteAsync(context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        return ((HttpStatusCode)context.Response.StatusCode, body);
    }

    private sealed class PersistingAdaptiveLearningService : IAdaptiveLearningService
    {
        private readonly ApiDbContext db;

        public PersistingAdaptiveLearningService(ApiDbContext db) => this.db = db;

        public async Task<AdaptiveSession> GeneratePracticeSessionAsync(string userId)
        {
            var question = await db.Questions
                .Include(x => x.Subtopic)
                .SingleAsync();

            if (question.Subtopic is null)
                throw new InvalidOperationException("Seeded adaptive question is missing subtopic metadata.");

            var session = new AdaptiveSession
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(35),
                ProfileDifficulty = AdaptiveDifficultyLevels.Medium,
                Items =
                [
                    new AdaptiveSessionItem
                    {
                        Id = Guid.NewGuid(),
                        AdaptiveSessionId = Guid.NewGuid(),
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

            session.Items[0].AdaptiveSessionId = session.Id;
            db.AdaptiveSessions.Add(session);

            await db.SaveChangesAsync();
            return session;
        }

        public Task<AdaptiveAnswerResult> SubmitAnswerAsync(string userId, AdaptiveAnswerRequest request) =>
            throw new NotSupportedException();

        public Task<List<AdaptiveRecommendation>> GetRecommendationsAsync(string userId) =>
            throw new NotSupportedException();

        public Task<List<ReviewItem>> GetDueReviewsAsync(string userId) =>
            throw new NotSupportedException();

        public Task DetectWeakTopicsAsync(string userId) =>
            Task.CompletedTask;
    }

    private sealed class SqliteFileTestDatabase : IAsyncDisposable
    {
        private readonly string filePath;
        private readonly string connectionString;

        private SqliteFileTestDatabase(string filePath, string connectionString)
        {
            this.filePath = filePath;
            this.connectionString = connectionString;
        }

        public static async Task<SqliteFileTestDatabase> CreateAsync()
        {
            var filePath = Path.Combine(Path.GetTempPath(), $"mathlearning-adaptive-start-{Guid.NewGuid():N}.db");
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
            return database;
        }

        public ApiDbContext CreateContext(params IInterceptor[] interceptors)
        {
            var builder = new DbContextOptionsBuilder<ApiDbContext>()
                .UseSqlite(connectionString);

            if (interceptors.Length > 0)
                builder.AddInterceptors(interceptors);

            return new ApiDbContext(builder.Options);
        }

        public async Task SeedAdaptiveCatalogAsync(ApiDbContext db)
        {
            if (await db.Categories.AnyAsync())
                return;

            var category = new Category("Adaptive Start Category");
            db.Categories.Add(category);
            await db.SaveChangesAsync();

            var topic = new Topic("Adaptive Start Topic");
            db.Topics.Add(topic);
            await db.SaveChangesAsync();

            var subtopic = new Subtopic("Adaptive Start Subtopic", topic.Id);
            db.Subtopics.Add(subtopic);
            await db.SaveChangesAsync();

            var question = new Question("1 + 1 = ?", 1, category.Id, "2");
            question.SetSubtopic(subtopic.Id);
            db.Questions.Add(question);
            await db.SaveChangesAsync();
        }

        public IServiceScope CreateVerificationScope()
        {
            var provider = new ServiceCollection()
                .AddLogging()
                .AddDbContext<ApiDbContext>(options => options.UseSqlite(connectionString))
                .BuildServiceProvider();

            return provider.CreateScope();
        }

        public ValueTask DisposeAsync()
        {
            DeleteIfExists(filePath);
            DeleteIfExists($"{filePath}-wal");
            DeleteIfExists($"{filePath}-shm");
            return ValueTask.CompletedTask;
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private sealed class CoordinatedAddedEntityInterceptor<TEntity> : SaveChangesInterceptor
        where TEntity : class
    {
        private readonly OrderedInsertCoordinator coordinator;
        private readonly int participant;
        private int used;

        public CoordinatedAddedEntityInterceptor(OrderedInsertCoordinator coordinator, int participant)
        {
            this.coordinator = coordinator;
            this.participant = participant;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref used, 1) == 0 && HasAddedEntity(eventData.Context))
                await coordinator.ArriveAndWaitAsync(participant, cancellationToken);

            return result;
        }

        private static bool HasAddedEntity(DbContext? context) =>
            context?.ChangeTracker.Entries<TEntity>().Any(x => x.State == EntityState.Added) == true;
    }

    private sealed class OrderedInsertCoordinator
    {
        private readonly TaskCompletionSource<bool> bothWritersArrived =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> releaseSecondWriter =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrivals;

        public async Task ArriveAndWaitAsync(int participant, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref arrivals) == 2)
                bothWritersArrived.TrySetResult(true);

            await bothWritersArrived.Task.WaitAsync(cancellationToken);

            if (participant == 2)
                await releaseSecondWriter.Task.WaitAsync(cancellationToken);
        }

        public Task WaitUntilBothWritersArriveAsync() => bothWritersArrived.Task;

        public void ReleaseSecondWriter() => releaseSecondWriter.TrySetResult(true);
    }
}
