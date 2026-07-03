using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MathLearning.Tests.Endpoints;

public sealed class OfflineBatchRelationalAtomicityTests
{
    [Fact]
    public async Task FailureAfterAnswerSave_RollsBackSessionStatsXpAndAnswerRows()
    {
        var factory = new RelationalOfflineBatchWebApplicationFactory();

        try
        {
            using var client = factory.CreateClient();
            var userId = $"offline-relational-{Guid.NewGuid():N}";
            await EnsureUserAsync(factory, userId);
            var correctAnswer = await GetCorrectAnswerTokenAsync(factory);
            var sessionId = Guid.NewGuid().ToString();
            var answeredAt = DateTime.UtcNow.AddMinutes(-10);

            factory.FailureInterceptor.Arm();
            var response = await PostAsUserAsync(
                client,
                userId,
                "/api/quiz/offline-submit",
                new
                {
                    sessionId,
                    answers = new[]
                    {
                        new
                        {
                            questionId = 1,
                            answer = correctAnswer,
                            timeSpent = 5,
                            isCorrectOffline = true,
                            answeredAt = answeredAt.ToString("O")
                        }
                    }
                });

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            var responseText = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain(RelationalOfflineBatchFailureInterceptor.SecretMessage, responseText, StringComparison.Ordinal);
            Assert.Equal(1, factory.FailureInterceptor.ThrowCount);

            using var verificationScope = factory.Services.CreateScope();
            var db = verificationScope.ServiceProvider.GetRequiredService<ApiDbContext>();

            Assert.False(await db.QuizSessions.AnyAsync(x => x.UserId == userId));
            Assert.False(await db.UserAnswers.AnyAsync(x => x.UserId == userId));
            Assert.False(await db.UserAnswerAudits.AnyAsync(x => x.UserId == userId));
            Assert.False(await db.UserQuestionStats.AnyAsync(x => x.UserId == userId));
            Assert.False(await db.UserXpEvents.AnyAsync(x => x.UserId == userId));

            var profile = await db.UserProfiles.SingleAsync(x => x.UserId == userId);
            Assert.Equal(0, profile.Xp);
            Assert.Equal(0, profile.DailyXp);
            Assert.Equal(0, profile.WeeklyXp);
            Assert.Equal(0, profile.MonthlyXp);
            Assert.Equal(1, profile.Level);
            Assert.Null(profile.LastActivityDay);
        }
        finally
        {
            factory.Dispose();
            factory.DeleteDatabaseFiles();
        }
    }

    [Fact]
    public async Task RetryAfterRolledBackFailure_ImportsExactlyOnceAndAwardsXpOnce()
    {
        var factory = new RelationalOfflineBatchWebApplicationFactory();

        try
        {
            using var client = factory.CreateClient();
            var userId = $"offline-relational-retry-{Guid.NewGuid():N}";
            await EnsureUserAsync(factory, userId);
            var correctAnswer = await GetCorrectAnswerTokenAsync(factory);
            var sessionId = Guid.NewGuid().ToString();
            var answeredAt = DateTime.UtcNow.AddMinutes(-15);
            var payload = new
            {
                sessionId,
                answers = new[]
                {
                    new
                    {
                        questionId = 1,
                        answer = correctAnswer,
                        timeSpent = 6,
                        isCorrectOffline = true,
                        answeredAt = answeredAt.ToString("O")
                    }
                }
            };

            factory.FailureInterceptor.Arm();
            var failed = await PostAsUserAsync(client, userId, "/api/quiz/offline-submit", payload);
            Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);

            factory.FailureInterceptor.Disarm();
            var retry = await PostAsUserAsync(client, userId, "/api/quiz/offline-submit", payload);
            Assert.Equal(HttpStatusCode.OK, retry.StatusCode);

            using var responseJson = JsonDocument.Parse(await retry.Content.ReadAsStringAsync());
            Assert.Equal(1, responseJson.RootElement.GetProperty("importedCount").GetInt32());
            Assert.Equal(10, responseJson.RootElement.GetProperty("newXp").GetInt32());

            var replay = await PostAsUserAsync(client, userId, "/api/quiz/offline-submit", payload);
            Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
            using var replayJson = JsonDocument.Parse(await replay.Content.ReadAsStringAsync());
            Assert.Equal(0, replayJson.RootElement.GetProperty("importedCount").GetInt32());
            Assert.Equal(10, replayJson.RootElement.GetProperty("newXp").GetInt32());

            using var verificationScope = factory.Services.CreateScope();
            var db = verificationScope.ServiceProvider.GetRequiredService<ApiDbContext>();
            Assert.Equal(1, await db.QuizSessions.CountAsync(x => x.UserId == userId));
            Assert.Equal(1, await db.UserAnswers.CountAsync(x => x.UserId == userId));
            Assert.Equal(1, await db.UserAnswerAudits.CountAsync(x => x.UserId == userId));

            var stat = await db.UserQuestionStats.SingleAsync(x => x.UserId == userId && x.QuestionId == 1);
            Assert.Equal(1, stat.Attempts);
            Assert.Equal(1, stat.CorrectAttempts);

            var profile = await db.UserProfiles.SingleAsync(x => x.UserId == userId);
            Assert.Equal(10, profile.Xp);
            Assert.Equal(10, profile.DailyXp);
            Assert.Equal(10, profile.WeeklyXp);
            Assert.Equal(10, profile.MonthlyXp);
        }
        finally
        {
            factory.Dispose();
            factory.DeleteDatabaseFiles();
        }
    }

    private static async Task<HttpResponseMessage> PostAsUserAsync(
        HttpClient client,
        string userId,
        string path,
        object payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Test-UserId", userId);
        return await client.SendAsync(request);
    }

    private static async Task EnsureUserAsync(
        RelationalOfflineBatchWebApplicationFactory factory,
        string userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        var createResult = await userManager.CreateAsync(new IdentityUser
        {
            Id = userId,
            UserName = userId,
            Email = $"{userId}@example.test"
        });
        Assert.True(createResult.Succeeded, string.Join(", ", createResult.Errors.Select(x => x.Description)));

        db.UserProfiles.Add(new UserProfile
        {
            UserId = userId,
            Username = userId,
            DisplayName = userId,
            Coins = 0,
            Xp = 0,
            DailyXp = 0,
            WeeklyXp = 0,
            MonthlyXp = 0,
            Level = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static async Task<string> GetCorrectAnswerTokenAsync(
        RelationalOfflineBatchWebApplicationFactory factory,
        int questionId = 1)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var question = await db.Questions
            .AsNoTracking()
            .Include(x => x.Options)
            .SingleAsync(x => x.Id == questionId);
        var correct = question.Options.Single(x => x.IsCorrect);
        return correct.Id.ToString();
    }
}

public sealed class RelationalOfflineBatchWebApplicationFactory : CustomWebApplicationFactory<Program>
{
    private readonly string databasePath =
        Path.Combine(Path.GetTempPath(), $"mathlearning-offline-batch-{Guid.NewGuid():N}.db");
    private readonly string connectionString;

    public RelationalOfflineBatchWebApplicationFactory()
    {
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false,
            DefaultTimeout = 30
        }.ToString();
    }

    public RelationalOfflineBatchFailureInterceptor FailureInterceptor { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApiDbContext>>();
            services.RemoveAll<ApiDbContext>();

            var options = new DbContextOptionsBuilder<ApiDbContext>()
                .UseSqlite(connectionString)
                .AddInterceptors(FailureInterceptor)
                .Options;

            services.AddSingleton(options);
            services.AddScoped<ApiDbContext>();
        });
    }

    public void DeleteDatabaseFiles()
    {
        DeleteIfExists(databasePath);
        DeleteIfExists($"{databasePath}-wal");
        DeleteIfExists($"{databasePath}-shm");
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}

public sealed class RelationalOfflineBatchFailureInterceptor : SaveChangesInterceptor
{
    public const string SecretMessage = "SECRET_OFFLINE_BATCH_AFTER_SAVE_FAILURE";

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

    public void Disarm()
    {
        matchingSaves.Clear();
        Interlocked.Exchange(ref armed, 0);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (Volatile.Read(ref armed) == 1 &&
            context is not null &&
            context.ChangeTracker.Entries<UserAnswer>().Any(x => x.State == EntityState.Added))
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
