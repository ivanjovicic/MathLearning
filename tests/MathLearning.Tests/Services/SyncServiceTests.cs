using System.Text.Json;
using MathLearning.Application.DTOs.Sync;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services;
using MathLearning.Infrastructure.Services.Sync;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MathLearning.Tests.Services;

public class SyncServiceTests
{
    [Fact]
    public async Task SyncAsync_SubmitAnswer_PersistsOperationAndReturnsCheckpoint()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var service = CreateSyncService(db, requireSignatures: true);

        var register = await service.RegisterDeviceAsync(
            "1",
            new RegisterSyncDeviceRequest("device-1", "Ivan phone", "android", "1.0.0"),
            CancellationToken.None);

        var question = await db.Questions
            .Include(x => x.Options)
            .FirstAsync(x => x.Id == 1);
        var correctAnswer = question.Options.First(x => x.IsCorrect).Text;
        var payload = JsonSerializer.SerializeToElement(new SubmitAnswerSyncPayloadDto(
            "session-1",
            1,
            correctAnswer,
            8,
            DateTime.UtcNow));

        var operation = new SyncOperationDto(
            Guid.NewGuid(),
            "device-1",
            "1",
            1,
            "submit_answer",
            DateTime.UtcNow,
            payload,
            null);

        operation = operation with
        {
            Signature = SyncService.BuildOperationSignature(register.DeviceSecret, operation)
        };

        var response = await service.SyncAsync(
            "1",
            new SyncRequestDto("device-1", 0, [operation]),
            CancellationToken.None);

        var answer = await db.UserAnswers.SingleAsync();
        var syncLog = await db.SyncEventLogs.SingleAsync();
        var deviceState = await db.DeviceSyncStates.SingleAsync(x => x.DeviceId == "device-1" && x.UserId == "1");

        Assert.Single(response.AcknowledgedOperations);
        Assert.Equal("Accepted", response.AcknowledgedOperations[0].Status);
        Assert.True(response.NewCheckpoint > 0);
        Assert.Single(response.ServerOperations);
        Assert.Equal(operation.OperationId, answer.SyncOperationId);
        Assert.Equal("Processed", syncLog.Status);
        Assert.Equal(1, deviceState.LastProcessedClientSequence);
    }

    [Fact]
    public async Task SyncAsync_ReplayedOperation_IsAcknowledgedWithStoredAckWithoutSecondEffect()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var service = CreateSyncService(db, requireSignatures: false);

        await service.RegisterDeviceAsync(
            "1",
            new RegisterSyncDeviceRequest("device-2", "Tablet", "ios", "1.0.0"),
            CancellationToken.None);

        var operationId = Guid.NewGuid();
        var operation = new SyncOperationDto(
            operationId,
            "device-2",
            "1",
            1,
            "submit_answer",
            DateTime.UtcNow,
            JsonSerializer.SerializeToElement(new SubmitAnswerSyncPayloadDto(
                "session-2",
                1,
                "2",
                5,
                DateTime.UtcNow)),
            null);

        await service.SyncAsync("1", new SyncRequestDto("device-2", 0, [operation]), CancellationToken.None);
        var replayResponse = await service.SyncAsync("1", new SyncRequestDto("device-2", 0, [operation]), CancellationToken.None);

        Assert.Single(replayResponse.AcknowledgedOperations);
        Assert.Equal("Accepted", replayResponse.AcknowledgedOperations[0].Status);
        Assert.Null(replayResponse.AcknowledgedOperations[0].ErrorCode);
        Assert.Equal(1, await db.UserAnswers.CountAsync());
        Assert.Equal(1, await db.SyncEventLogs.CountAsync());
    }

    [Fact]
    public async Task SyncAsync_SameOperationIdAcrossUsers_RemainsIsolated()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        db.Users.Add(new Microsoft.AspNetCore.Identity.IdentityUser
        {
            Id = "2",
            UserName = "otheruser",
            Email = "otheruser@example.com"
        });
        db.UserProfiles.Add(new global::MathLearning.Domain.Entities.UserProfile
        {
            UserId = "2",
            Username = "otheruser",
            DisplayName = "Other User",
            Coins = 0,
            Level = 1,
            Xp = 0,
            Streak = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = CreateSyncService(db, requireSignatures: false);

        await service.RegisterDeviceAsync(
            "1",
            new RegisterSyncDeviceRequest("device-scope-1", "Phone", "android", "1.0.0"),
            CancellationToken.None);
        await service.RegisterDeviceAsync(
            "2",
            new RegisterSyncDeviceRequest("device-scope-2", "Tablet", "ios", "1.0.0"),
            CancellationToken.None);

        var operationId = Guid.NewGuid();
        var payload = JsonSerializer.SerializeToElement(new SubmitAnswerSyncPayloadDto(
            "session-scope",
            1,
            "2",
            6,
            DateTime.UtcNow));

        var userOneOperation = new SyncOperationDto(
            operationId,
            "device-scope-1",
            "1",
            1,
            "submit_answer",
            DateTime.UtcNow,
            payload,
            null);

        var userTwoOperation = new SyncOperationDto(
            operationId,
            "device-scope-2",
            "2",
            1,
            "submit_answer",
            DateTime.UtcNow,
            payload,
            null);

        var firstResponse = await service.SyncAsync("1", new SyncRequestDto("device-scope-1", 0, [userOneOperation]), CancellationToken.None);
        var secondResponse = await service.SyncAsync("2", new SyncRequestDto("device-scope-2", 0, [userTwoOperation]), CancellationToken.None);

        Assert.Equal("Accepted", firstResponse.AcknowledgedOperations.Single().Status);
        Assert.Equal("Accepted", secondResponse.AcknowledgedOperations.Single().Status);
        Assert.Equal(2, await db.SyncEventLogs.CountAsync());
        Assert.Equal(2, await db.UserAnswers.CountAsync());
    }

    [Fact]
    public async Task SyncAsync_SameScopedOperationWithDifferentPayload_ReturnsConflictWithoutEffects()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var service = CreateSyncService(db, requireSignatures: false);

        await service.RegisterDeviceAsync(
            "1",
            new RegisterSyncDeviceRequest("device-conflict", "Conflict phone", "android", "1.0.0"),
            CancellationToken.None);

        var operationId = Guid.NewGuid();
        var acceptedOperation = new SyncOperationDto(
            operationId,
            "device-conflict",
            "1",
            1,
            "submit_answer",
            DateTime.UtcNow,
            JsonSerializer.SerializeToElement(new SubmitAnswerSyncPayloadDto(
                "session-conflict",
                1,
                "2",
                5,
                DateTime.UtcNow)),
            null);

        var acceptedResponse = await service.SyncAsync("1", new SyncRequestDto("device-conflict", 0, [acceptedOperation]), CancellationToken.None);
        Assert.Equal("Accepted", acceptedResponse.AcknowledgedOperations.Single().Status);

        var conflictingOperation = acceptedOperation with
        {
            Payload = JsonSerializer.SerializeToElement(new SubmitAnswerSyncPayloadDto(
                "session-conflict",
                1,
                "1",
                5,
                DateTime.UtcNow))
        };

        var conflictResponse = await service.SyncAsync("1", new SyncRequestDto("device-conflict", 0, [conflictingOperation]), CancellationToken.None);

        Assert.Equal("Conflict", conflictResponse.AcknowledgedOperations.Single().Status);
        Assert.Equal("payload_conflict", conflictResponse.AcknowledgedOperations.Single().ErrorCode);
        Assert.Equal(1, await db.UserAnswers.CountAsync());
        Assert.Equal(1, await db.SyncEventLogs.CountAsync());
    }

    [Fact]
    public async Task SyncAsync_MultipleChoiceIgnoresLegacyCorrectAnswerFallbackWhenCanonicalOptionExists()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var service = CreateSyncService(db, requireSignatures: false);

        await service.RegisterDeviceAsync(
            "1",
            new RegisterSyncDeviceRequest("device-legacy", "Legacy phone", "android", "1.0.0"),
            CancellationToken.None);

        var question = await db.Questions
            .Include(x => x.Options)
            .FirstAsync(x => x.Id == 1);
        question.SetCorrectAnswer("legacy-stale-answer");
        await db.SaveChangesAsync();

        var operation = new SyncOperationDto(
            Guid.NewGuid(),
            "device-legacy",
            "1",
            1,
            "submit_answer",
            DateTime.UtcNow,
            JsonSerializer.SerializeToElement(new SubmitAnswerSyncPayloadDto(
                "session-legacy",
                1,
                "legacy-stale-answer",
                5,
                DateTime.UtcNow)),
            null);

        var response = await service.SyncAsync("1", new SyncRequestDto("device-legacy", 0, [operation]), CancellationToken.None);

        var answer = await db.UserAnswers.SingleAsync(x => x.UserId == "1" && x.DeviceId == "device-legacy" && x.SyncOperationId == operation.OperationId);

        Assert.Single(response.AcknowledgedOperations);
        Assert.Equal("Accepted", response.AcknowledgedOperations[0].Status);
        Assert.False(answer.IsCorrect);
    }

    [Fact]
    public async Task SyncAsync_UserMismatch_IsRejectedWithoutPersistingAnswer()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var service = CreateSyncService(db, requireSignatures: false);

        await service.RegisterDeviceAsync(
            "1",
            new RegisterSyncDeviceRequest("device-4", "Mismatch phone", "android", "1.0.0"),
            CancellationToken.None);

        var operation = new SyncOperationDto(
            Guid.NewGuid(),
            "device-4",
            "different-user",
            1,
            "submit_answer",
            DateTime.UtcNow,
            JsonSerializer.SerializeToElement(new SubmitAnswerSyncPayloadDto(
                "session-user-mismatch",
                1,
                "2",
                6,
                DateTime.UtcNow)),
            null);

        var response = await service.SyncAsync(
            "1",
            new SyncRequestDto("device-4", 0, [operation]),
            CancellationToken.None);

        Assert.Single(response.AcknowledgedOperations);
        Assert.Equal("Rejected", response.AcknowledgedOperations[0].Status);
        Assert.Equal("user_mismatch", response.AcknowledgedOperations[0].ErrorCode);
        Assert.Equal(0, await db.UserAnswers.CountAsync());
        Assert.Equal(0, await db.SyncEventLogs.CountAsync());
    }

    [Fact]
    public async Task RedriveDeadLetterAsync_ReprocessesPendingDeadLetter()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var service = CreateSyncService(db, requireSignatures: false);

        await service.RegisterDeviceAsync(
            "1",
            new RegisterSyncDeviceRequest("device-3", "Offline phone", "android", "1.0.0"),
            CancellationToken.None);

        var operationId = Guid.NewGuid();
        var deadLetterId = Guid.NewGuid();
        var payloadJson = JsonSerializer.Serialize(new SubmitAnswerSyncPayloadDto(
            "session-redrive",
            1,
            "2",
            7,
            DateTime.UtcNow));
        var payloadHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payloadJson)));

        db.SyncEventLogs.Add(new global::MathLearning.Domain.Entities.SyncEventLog
        {
            OperationId = operationId,
            DeviceId = "device-3",
            UserId = "1",
            ClientSequence = 1,
            OperationType = "submit_answer",
            PayloadHash = payloadHash,
            PayloadJson = payloadJson,
            Status = global::MathLearning.Domain.Entities.SyncEventStatuses.DeadLettered,
            OccurredAtUtc = DateTime.UtcNow,
            ReceivedAtUtc = DateTime.UtcNow,
            RetryCount = 5,
            ErrorCode = "processing_failed",
            ErrorMessage = "Synthetic failure"
        });

        db.SyncDeadLetters.Add(new global::MathLearning.Domain.Entities.SyncDeadLetter
        {
            Id = deadLetterId,
            OperationId = operationId,
            DeviceId = "device-3",
            UserId = "1",
            OperationType = "submit_answer",
            PayloadHash = payloadHash,
            PayloadJson = payloadJson,
            RetryCount = 5,
            Status = global::MathLearning.Domain.Entities.SyncDeadLetterStatuses.Pending,
            FailureReason = "Synthetic failure",
            CreatedAtUtc = DateTime.UtcNow,
            LastFailedAtUtc = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var result = await service.RedriveDeadLetterAsync(deadLetterId, "admin-user", CancellationToken.None);

        var deadLetter = await db.SyncDeadLetters.SingleAsync(x => x.Id == deadLetterId);
        var log = await db.SyncEventLogs.SingleAsync(x => x.OperationId == operationId);

        Assert.Equal(global::MathLearning.Domain.Entities.SyncDeadLetterStatuses.Resolved, result.Status);
        Assert.Equal(global::MathLearning.Domain.Entities.SyncDeadLetterStatuses.Resolved, deadLetter.Status);
        Assert.NotNull(deadLetter.ResolvedAtUtc);
        Assert.Equal(global::MathLearning.Domain.Entities.SyncEventStatuses.Processed, log.Status);
        Assert.Equal(1, await db.UserAnswers.CountAsync(x => x.UserId == "1" && x.DeviceId == "device-3" && x.SyncOperationId == operationId));
    }

    private static SyncService CreateSyncService(ApiDbContext db, bool requireSignatures)
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
            Options.Create(new SyncOptions
            {
                RequireOperationSignatures = requireSignatures,
                MaxBatchSize = 100,
                MaxServerEventsPerSync = 100,
                MaxProcessingRetries = 3,
                DefaultQuestionBundleSize = 50
            }),
            new SyncMetricsService(),
            NullLogger<SyncService>.Instance);
    }
}
