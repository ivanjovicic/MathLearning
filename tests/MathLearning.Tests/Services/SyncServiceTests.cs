using System.Text.Json;
using MathLearning.Application.DTOs.Sync;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services;
using MathLearning.Infrastructure.Services.Leaderboard;
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
    public async Task SyncAsync_ReplayedOperation_IsAcknowledgedAsDuplicateWithoutSecondEffect()
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
        Assert.Equal("Duplicate", replayResponse.AcknowledgedOperations[0].Status);
        Assert.Equal(1, await db.UserAnswers.CountAsync());
        Assert.Equal(1, await db.SyncEventLogs.CountAsync());
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
        var payloadJson = JsonSerializer.Serialize(new SubmitAnswerSyncPayloadDto(
            "session-redrive",
            1,
            "2",
            7,
            DateTime.UtcNow));

        db.SyncEventLogs.Add(new Domain.Entities.SyncEventLog
        {
            OperationId = operationId,
            DeviceId = "device-3",
            UserId = "1",
            ClientSequence = 1,
            OperationType = "submit_answer",
            PayloadJson = payloadJson,
            Status = Domain.Entities.SyncEventStatuses.DeadLettered,
            OccurredAtUtc = DateTime.UtcNow,
            ReceivedAtUtc = DateTime.UtcNow,
            RetryCount = 5,
            ErrorCode = "processing_failed",
            ErrorMessage = "Synthetic failure"
        });

        db.SyncDeadLetters.Add(new Domain.Entities.SyncDeadLetter
        {
            OperationId = operationId,
            DeviceId = "device-3",
            UserId = "1",
            OperationType = "submit_answer",
            PayloadJson = payloadJson,
            RetryCount = 5,
            Status = Domain.Entities.SyncDeadLetterStatuses.Pending,
            FailureReason = "Synthetic failure",
            CreatedAtUtc = DateTime.UtcNow,
            LastFailedAtUtc = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var result = await service.RedriveDeadLetterAsync(operationId, "admin-user", CancellationToken.None);

        var deadLetter = await db.SyncDeadLetters.SingleAsync(x => x.OperationId == operationId);
        var log = await db.SyncEventLogs.SingleAsync(x => x.OperationId == operationId);

        Assert.Equal(Domain.Entities.SyncDeadLetterStatuses.Resolved, result.Status);
        Assert.Equal(Domain.Entities.SyncDeadLetterStatuses.Resolved, deadLetter.Status);
        Assert.NotNull(deadLetter.ResolvedAtUtc);
        Assert.Equal(Domain.Entities.SyncEventStatuses.Processed, log.Status);
        Assert.Equal(1, await db.UserAnswers.CountAsync(x => x.SyncOperationId == operationId));
    }

    private static SyncService CreateSyncService(ApiDbContext db, bool requireSignatures)
    {
        var aggregationService = new SchoolLeaderboardAggregationService(db);
        var xpTrackingService = new XpTrackingService(db, aggregationService, null);

        return new SyncService(
            db,
            xpTrackingService,
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
