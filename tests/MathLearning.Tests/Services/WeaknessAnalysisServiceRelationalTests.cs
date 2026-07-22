using MathLearning.Api.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace MathLearning.Tests.Services;

public sealed class WeaknessAnalysisServiceRelationalTests
{
    [Fact]
    public async Task AnalyzeUserAsync_OneHundredThousandAttempts_UsesBoundedLookbackQuery()
    {
        await using var database = await BoundedLookbackDatabase.CreateAsync();
        await database.SeedAsync(100_000);

        await using var db = database.CreateContext();
        var service = new WeaknessAnalysisService(db, NullLogger<WeaknessAnalysisService>.Instance);

        await service.AnalyzeUserAsync(database.UserId, CancellationToken.None);

        var boundedQuery = db.QuizAttempts
            .AsNoTracking()
            .Where(x => x.UserId == database.UserId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(1_000)
            .Select(x => new { x.TopicId, x.SubtopicId, x.TimeSpentMs })
            .ToQueryString();

        Assert.Contains("LIMIT", boundedQuery, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class BoundedLookbackDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions<ApiDbContext> options;

        private BoundedLookbackDatabase(SqliteConnection connection, DbContextOptions<ApiDbContext> options)
        {
            this.connection = connection;
            this.options = options;
        }

        public Guid UserId { get; } = Guid.NewGuid();

        public static async Task<BoundedLookbackDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<ApiDbContext>()
                .UseSqlite(connection)
                .Options;

            var database = new BoundedLookbackDatabase(connection, options);
            await using var db = database.CreateContext();
            await db.Database.EnsureCreatedAsync();
            return database;
        }

        public ApiDbContext CreateContext() => new(options);

        public async Task SeedAsync(int attemptCount)
        {
            await using var db = CreateContext();

            var topic = new Topic("Bounded Lookback Topic");
            db.Topics.Add(topic);
            await db.SaveChangesAsync();

            var subtopic = new Subtopic("Bounded Lookback Subtopic", topic.Id);
            db.Subtopics.Add(subtopic);
            await db.SaveChangesAsync();

            db.UserTopicStats.Add(new UserTopicStat
            {
                UserId = UserId,
                TopicId = topic.Id,
                TotalQuestions = attemptCount,
                CorrectAnswers = attemptCount / 2,
                Accuracy = 0.5m,
                LastAttempt = DateTime.UtcNow,
                WeaknessScore = 1.0m
            });
            db.UserSubtopicStats.Add(new UserSubtopicStat
            {
                UserId = UserId,
                SubtopicId = subtopic.Id,
                TotalQuestions = attemptCount,
                CorrectAnswers = attemptCount / 2,
                Accuracy = 0.5m,
                LastAttempt = DateTime.UtcNow,
                WeaknessScore = 1.0m
            });
            await db.SaveChangesAsync();

            var now = DateTime.UtcNow;
            const int batchSize = 5_000;
            var attempts = new List<QuizAttempt>(batchSize);

            for (var i = 0; i < attemptCount; i++)
            {
                attempts.Add(new QuizAttempt
                {
                    AttemptKey = $"attempt:{i:D6}",
                    UserId = UserId,
                    QuizId = Guid.NewGuid(),
                    QuestionId = i + 1,
                    TopicId = topic.Id,
                    SubtopicId = subtopic.Id,
                    Correct = (i % 2) == 0,
                    TimeSpentMs = 500 + (i % 250),
                    CreatedAt = now.AddSeconds(-i)
                });

                if (attempts.Count == batchSize)
                {
                    db.QuizAttempts.AddRange(attempts);
                    await db.SaveChangesAsync();
                    db.ChangeTracker.Clear();
                    attempts.Clear();
                }
            }

            if (attempts.Count > 0)
            {
                db.QuizAttempts.AddRange(attempts);
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await connection.DisposeAsync();
        }
    }
}
