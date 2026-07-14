using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Endpoints;

public sealed class ProgressSyncIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;

    public ProgressSyncIntegrationTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task LegacyCompletedPayload_IsRejected_AndWritesNothing()
    {
        var userId = NewUserId("legacy");
        await EnsureUserAsync(userId);

        var response = await PostProgressSyncAsync(
            userId,
            new
            {
                completed = true,
                day = DateOnly.FromDateTime(DateTime.UtcNow).ToString("O")
            });

        Assert.Equal(HttpStatusCode.UpgradeRequired, response.StatusCode);
        var payload = await ReadJsonAsync(response);
        Assert.Equal("progress_sync_legacy_client", payload.GetProperty("errorCode").GetString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        Assert.False(await db.UserDailyStats.AnyAsync(x => x.UserId == userId));
    }

    [Fact]
    public async Task SettledEvidence_CompletesDay_AndReplayIsStable()
    {
        var userId = NewUserId("settled");
        var day = DateOnly.FromDateTime(DateTime.UtcNow);
        var evidence = await SeedSettledAnswerEvidenceAsync(userId, day);

        var request = new
        {
            operationId = "progress-sync-settled-op",
            idempotencyKey = "progress-sync-settled-key",
            deviceId = evidence.DeviceId,
            day = day.ToString("O"),
            quizOperationIds = new[] { evidence.OperationId }
        };

        var first = await PostProgressSyncAsync(userId, request);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstPayload = await ReadJsonAsync(first);
        Assert.False(firstPayload.GetProperty("alreadyProcessed").GetBoolean());
        Assert.True(firstPayload.GetProperty("completed").GetBoolean());
        Assert.Equal(1, firstPayload.GetProperty("settledEvidenceCount").GetInt32());

        var replay = await PostProgressSyncAsync(userId, request);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayPayload = await ReadJsonAsync(replay);
        Assert.True(replayPayload.GetProperty("alreadyProcessed").GetBoolean());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var stat = await db.UserDailyStats.SingleAsync(x => x.UserId == userId && x.Day == day);
        Assert.True(stat.Completed);
        Assert.Equal(1, await db.UserDailyStats.CountAsync(x => x.UserId == userId));
        Assert.Equal(1, await db.IdempotencyLedgers.CountAsync(x =>
            x.UserId == userId &&
            x.OperationType == "progress_sync"));
    }

    [Fact]
    public async Task SameKeyDifferentEvidence_ReturnsConflict()
    {
        var userId = NewUserId("conflict");
        var day = DateOnly.FromDateTime(DateTime.UtcNow);
        var firstEvidence = await SeedSettledAnswerEvidenceAsync(userId, day, suffix: "a");
        var secondEvidence = await SeedSettledAnswerEvidenceAsync(userId, day, suffix: "b");

        var first = await PostProgressSyncAsync(userId, new
        {
            operationId = "progress-sync-conflict-op",
            idempotencyKey = "progress-sync-conflict-key",
            deviceId = firstEvidence.DeviceId,
            day = day.ToString("O"),
            quizOperationIds = new[] { firstEvidence.OperationId }
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var conflict = await PostProgressSyncAsync(userId, new
        {
            operationId = "progress-sync-conflict-op",
            idempotencyKey = "progress-sync-conflict-key",
            deviceId = secondEvidence.DeviceId,
            day = day.ToString("O"),
            quizOperationIds = new[] { secondEvidence.OperationId }
        });

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var payload = await ReadJsonAsync(conflict);
        Assert.Equal("idempotency_conflict", payload.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task EvidenceFromOtherDevice_IsRejected()
    {
        var userId = NewUserId("scope");
        var day = DateOnly.FromDateTime(DateTime.UtcNow);
        var evidence = await SeedSettledAnswerEvidenceAsync(userId, day);
        var otherDeviceId = $"device-other-{Guid.NewGuid():N}";
        await EnsureDeviceAsync(userId, otherDeviceId);

        var response = await PostProgressSyncAsync(userId, new
        {
            operationId = "progress-sync-scope-op",
            idempotencyKey = "progress-sync-scope-key",
            deviceId = otherDeviceId,
            day = day.ToString("O"),
            quizOperationIds = new[] { evidence.OperationId }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await ReadJsonAsync(response);
        Assert.Equal("progress_sync_evidence_scope_mismatch", payload.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task FutureAndOutOfWindowDays_AreRejected()
    {
        var userId = NewUserId("window");

        var futureDay = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var futureEvidence = await SeedSettledAnswerEvidenceAsync(userId, futureDay, suffix: "future");
        var futureResponse = await PostProgressSyncAsync(userId, new
        {
            operationId = "progress-sync-future-op",
            idempotencyKey = "progress-sync-future-key",
            deviceId = futureEvidence.DeviceId,
            day = futureDay.ToString("O"),
            quizOperationIds = new[] { futureEvidence.OperationId }
        });
        Assert.Equal(HttpStatusCode.BadRequest, futureResponse.StatusCode);
        var futurePayload = await ReadJsonAsync(futureResponse);
        Assert.Equal("progress_sync_future_date", futurePayload.GetProperty("errorCode").GetString());

        var oldDay = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-31));
        var oldEvidence = await SeedSettledAnswerEvidenceAsync(userId, oldDay, suffix: "old");
        var oldResponse = await PostProgressSyncAsync(userId, new
        {
            operationId = "progress-sync-old-op",
            idempotencyKey = "progress-sync-old-key",
            deviceId = oldEvidence.DeviceId,
            day = oldDay.ToString("O"),
            quizOperationIds = new[] { oldEvidence.OperationId }
        });
        Assert.Equal(HttpStatusCode.BadRequest, oldResponse.StatusCode);
        var oldPayload = await ReadJsonAsync(oldResponse);
        Assert.Equal("progress_sync_out_of_window", oldPayload.GetProperty("errorCode").GetString());
    }

    private async Task<(string DeviceId, Guid OperationId)> SeedSettledAnswerEvidenceAsync(
        string userId,
        DateOnly day,
        string suffix = "default")
    {
        var deviceId = $"device-{suffix}-{Guid.NewGuid():N}";
        var operationId = Guid.NewGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        await EnsureUserAsync(userId);
        await EnsureDeviceAsync(userId, deviceId);

        var question = await db.Questions
            .Include(x => x.Options)
            .FirstAsync(x => x.Id == 1);
        var answer = question.Options.First(x => x.IsCorrect).Text;
        var answeredAt = day.ToDateTime(new TimeOnly(10, 0), DateTimeKind.Utc);
        var sessionId = Guid.NewGuid();

        db.QuizSessions.Add(new QuizSession
        {
            Id = sessionId,
            UserId = userId,
            StartedAt = answeredAt
        });

        db.SyncEventLogs.Add(new SyncEventLog
        {
            OperationId = operationId,
            DeviceId = deviceId,
            UserId = userId,
            ClientSequence = 1,
            OperationType = "submit_answer",
            PayloadJson = "{}",
            Status = SyncEventStatuses.Processed,
            OccurredAtUtc = answeredAt,
            ReceivedAtUtc = answeredAt,
            ProcessedAtUtc = answeredAt
        });

        db.UserAnswers.Add(new UserAnswer
        {
            SyncOperationId = operationId,
            DeviceId = deviceId,
            ClientSequence = 1,
            UserId = userId,
            QuestionId = question.Id,
            QuizSessionId = sessionId,
            Answer = answer,
            IsCorrect = true,
            TimeSpentSeconds = 12,
            AnsweredAt = answeredAt
        });

        await db.SaveChangesAsync();

        return (deviceId, operationId);
    }

    private async Task EnsureUserAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        if (!await db.Users.AnyAsync(x => x.Id == userId))
        {
            db.Users.Add(new IdentityUser
            {
                Id = userId,
                UserName = userId,
                Email = $"{userId}@example.test"
            });
        }

        if (!await db.UserProfiles.AnyAsync(x => x.UserId == userId))
        {
            db.UserProfiles.Add(new UserProfile
            {
                UserId = userId,
                Username = userId,
                DisplayName = userId,
                Coins = 100,
                Level = 1,
                Xp = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }

    private async Task EnsureDeviceAsync(string userId, string deviceId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        if (!await db.SyncDevices.AnyAsync(x => x.UserId == userId && x.DeviceId == deviceId))
        {
            db.SyncDevices.Add(new SyncDevice
            {
                DeviceId = deviceId,
                UserId = userId,
                DeviceName = "Test Device",
                Platform = "android",
                AppVersion = "1.0.0",
                SecretKey = "test-secret",
                Status = SyncDeviceStatuses.Active,
                RegisteredAtUtc = DateTime.UtcNow,
                LastSeenAtUtc = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }

    private async Task<HttpResponseMessage> PostProgressSyncAsync(string userId, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/progress/sync")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("X-Test-UserId", userId);

        return await _client.SendAsync(request);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static string NewUserId(string suffix) => $"progress-sync-{suffix}-{Guid.NewGuid():N}";
}
