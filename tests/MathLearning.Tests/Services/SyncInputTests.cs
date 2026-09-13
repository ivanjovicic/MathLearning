using System.Text.Json;
using MathLearning.Application.DTOs.Sync;
using MathLearning.Application.Services;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services;
using MathLearning.Infrastructure.Services.Sync;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MathLearning.Tests.Services;

public sealed class SyncInputTests
{
    [Fact]
    public async Task SyncAsync_RejectsTooManyOperations_BeforeDatabaseWork()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var service = CreateSyncService(db, new SyncOptions
        {
            RequireOperationSignatures = false,
            MaxOperationsPerBatch = 1
        });

        var request = new SyncRequestDto(
            "device-boundary",
            0,
            [CreateOperation("device-boundary", "1"), CreateOperation("device-boundary", "2")]);

        var ex = await Assert.ThrowsAsync<SyncRequestValidationException>(() => service.SyncAsync("1", request, CancellationToken.None));

        Assert.Equal(422, ex.StatusCode);
        Assert.Equal("batch_too_large", ex.ErrorCode);
        Assert.Equal(0, await db.SyncEventLogs.CountAsync());
        Assert.Equal(0, await db.SyncDeadLetters.CountAsync());
    }

    [Fact]
    public async Task SyncAsync_RejectsOversizedPayload_BeforeDatabaseWork()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var service = CreateSyncService(db, new SyncOptions
        {
            RequireOperationSignatures = false,
            MaxOperationPayloadBytes = 32
        });

        var request = new SyncRequestDto(
            "device-boundary",
            0,
            [CreateOperation("device-boundary", "1", payload: JsonSerializer.SerializeToElement(new
            {
                sessionId = "payload-size-session",
                questionId = 1,
                answer = new string('x', 128),
                timeSpentSeconds = 5,
                answeredAtUtc = DateTime.UtcNow
            }))]);

        var ex = await Assert.ThrowsAsync<SyncRequestValidationException>(() => service.SyncAsync("1", request, CancellationToken.None));

        Assert.Equal(422, ex.StatusCode);
        Assert.Equal("payload_too_large", ex.ErrorCode);
        Assert.Equal(0, await db.SyncEventLogs.CountAsync());
        Assert.Equal(0, await db.SyncDeadLetters.CountAsync());
    }

    [Fact]
    public async Task SyncAsync_RejectsUnknownOperationType_BeforeDatabaseWork()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var service = CreateSyncService(db, new SyncOptions
        {
            RequireOperationSignatures = false
        });

        var request = new SyncRequestDto(
            "device-boundary",
            0,
            [new SyncOperationDto(
                Guid.NewGuid(),
                "device-boundary",
                "1",
                1,
                "unsupported_operation",
                DateTime.UtcNow,
                JsonSerializer.SerializeToElement(new { sessionId = "session-1", questionId = 1, answer = "a", timeSpentSeconds = 5, answeredAtUtc = DateTime.UtcNow }),
                null)]);

        var ex = await Assert.ThrowsAsync<SyncRequestValidationException>(() => service.SyncAsync("1", request, CancellationToken.None));

        Assert.Equal(422, ex.StatusCode);
        Assert.Equal("unsupported_operation", ex.ErrorCode);
        Assert.Equal(0, await db.SyncEventLogs.CountAsync());
        Assert.Equal(0, await db.SyncDeadLetters.CountAsync());
    }

    [Fact]
    public async Task SyncAsync_RejectsOverlongSignature_BeforeDatabaseWork()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var service = CreateSyncService(db, new SyncOptions
        {
            RequireOperationSignatures = true,
            MaxSignatureBytes = 8
        });

        var request = new SyncRequestDto(
            "device-boundary",
            0,
            [new SyncOperationDto(
                Guid.NewGuid(),
                "device-boundary",
                "1",
                1,
                "submit_answer",
                DateTime.UtcNow,
                JsonSerializer.SerializeToElement(new { sessionId = "session-1", questionId = 1, answer = "a", timeSpentSeconds = 5, answeredAtUtc = DateTime.UtcNow }),
                new string('s', 16))]);

        var ex = await Assert.ThrowsAsync<SyncRequestValidationException>(() => service.SyncAsync("1", request, CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal("signature_too_long", ex.ErrorCode);
        Assert.Equal(0, await db.SyncEventLogs.CountAsync());
        Assert.Equal(0, await db.SyncDeadLetters.CountAsync());
    }

    private static SyncService CreateSyncService(ApiDbContext db, SyncOptions options)
    {
        var xpTrackingService = new XpTrackingService(
            db,
            Options.Create(new XpTrackingOptions()),
            NullLogger<XpTrackingService>.Instance,
            null);

        return new SyncService(
            db,
            xpTrackingService,
            new NoOpAnswerPatternAntiCheatService(),
            Options.Create(options),
            new SyncMetricsService(),
            NullLogger<SyncService>.Instance);
    }

    private static SyncOperationDto CreateOperation(string deviceId, string userId, JsonElement? payload = null)
        => new(
            Guid.NewGuid(),
            deviceId,
            userId,
            1,
            "submit_answer",
            DateTime.UtcNow,
            payload ?? JsonSerializer.SerializeToElement(new
            {
                sessionId = "session-boundary",
                questionId = 1,
                answer = "a",
                timeSpentSeconds = 5,
                answeredAtUtc = DateTime.UtcNow
            }),
            null);
}
