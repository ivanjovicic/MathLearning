using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services.Sync;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MathLearning.Tests.Services;

public sealed class SyncRetentionTests
{
    [Fact]
    public async Task CleanupAsync_DeletesOnlyEligibleRowsInBoundedBatches()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        SeedRetainedRows(db);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = CreateRetentionService(db, new SyncOptions
        {
            RetentionBatchSize = 1,
            SyncEventLogRetentionDays = 1,
            ServerSyncEventRetentionDays = 1,
            SyncDeadLetterRetentionDays = 1
        });

        var result = await service.CleanupAsync(CancellationToken.None);

        Assert.Equal(1, result.DeletedSyncEventLogs);
        Assert.Equal(1, result.DeletedServerSyncEvents);
        Assert.Equal(1, result.DeletedSyncDeadLetters);

        Assert.Equal(3, await db.SyncEventLogs.CountAsync());
        Assert.Equal(3, await db.ServerSyncEvents.CountAsync());
        Assert.Equal(2, await db.SyncDeadLetters.CountAsync());

        Assert.Contains(await db.SyncEventLogs.ToListAsync(), x => x.Status == SyncEventStatuses.Failed);
        Assert.Contains(await db.SyncDeadLetters.ToListAsync(), x => x.Status == SyncDeadLetterStatuses.Pending);
    }

    [Fact]
    public async Task CleanupAsync_CancellationStopsBeforeDeleting()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        SeedRetainedRows(db);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = CreateRetentionService(db, new SyncOptions
        {
            RetentionBatchSize = 1,
            SyncEventLogRetentionDays = 1,
            ServerSyncEventRetentionDays = 1,
            SyncDeadLetterRetentionDays = 1
        });

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.CleanupAsync(cts.Token));

        Assert.Equal(4, await db.SyncEventLogs.CountAsync());
        Assert.Equal(4, await db.ServerSyncEvents.CountAsync());
        Assert.Equal(3, await db.SyncDeadLetters.CountAsync());
    }

    private static SyncRetentionService CreateRetentionService(ApiDbContext db, SyncOptions options)
        => new(
            db,
            Options.Create(options),
            new SyncMetricsService(),
            NullLogger<SyncRetentionService>.Instance);

    private static void SeedRetainedRows(ApiDbContext db)
    {
        var old = DateTime.UtcNow.AddDays(-7);
        var recent = DateTime.UtcNow.AddHours(-1);

        db.SyncEventLogs.AddRange(
            new SyncEventLog
            {
                OperationId = Guid.NewGuid(),
                DeviceId = "device-retention",
                UserId = "1",
                ClientSequence = 1,
                OperationType = "submit_answer",
                PayloadHash = "A",
                PayloadJson = "{}",
                Status = SyncEventStatuses.Processed,
                OccurredAtUtc = old,
                ReceivedAtUtc = old,
                ProcessedAtUtc = old
            },
            new SyncEventLog
            {
                OperationId = Guid.NewGuid(),
                DeviceId = "device-retention",
                UserId = "1",
                ClientSequence = 2,
                OperationType = "submit_answer",
                PayloadHash = "B",
                PayloadJson = "{}",
                Status = SyncEventStatuses.Processed,
                OccurredAtUtc = old.AddMinutes(1),
                ReceivedAtUtc = old.AddMinutes(1),
                ProcessedAtUtc = old.AddMinutes(1)
            },
            new SyncEventLog
            {
                OperationId = Guid.NewGuid(),
                DeviceId = "device-retention",
                UserId = "1",
                ClientSequence = 3,
                OperationType = "submit_answer",
                PayloadHash = "C",
                PayloadJson = "{}",
                Status = SyncEventStatuses.Processed,
                OccurredAtUtc = recent,
                ReceivedAtUtc = recent,
                ProcessedAtUtc = recent
            },
            new SyncEventLog
            {
                OperationId = Guid.NewGuid(),
                DeviceId = "device-retention",
                UserId = "1",
                ClientSequence = 4,
                OperationType = "submit_answer",
                PayloadHash = "D",
                PayloadJson = "{}",
                Status = SyncEventStatuses.Failed,
                OccurredAtUtc = old,
                ReceivedAtUtc = old,
                RetryCount = 1
            });

        db.ServerSyncEvents.AddRange(
            new ServerSyncEvent
            {
                UserId = "1",
                DeviceId = "device-retention",
                EventType = "answer_processed",
                AggregateType = "question",
                AggregateId = "1",
                PayloadJson = "{}",
                CreatedAtUtc = old
            },
            new ServerSyncEvent
            {
                UserId = "1",
                DeviceId = "device-retention",
                EventType = "answer_processed",
                AggregateType = "question",
                AggregateId = "2",
                PayloadJson = "{}",
                CreatedAtUtc = old.AddMinutes(1)
            },
            new ServerSyncEvent
            {
                UserId = "1",
                DeviceId = "device-retention",
                EventType = "answer_processed",
                AggregateType = "question",
                AggregateId = "3",
                PayloadJson = "{}",
                CreatedAtUtc = recent
            },
            new ServerSyncEvent
            {
                UserId = "1",
                DeviceId = "device-retention",
                EventType = "answer_processed",
                AggregateType = "question",
                AggregateId = "4",
                PayloadJson = "{}",
                CreatedAtUtc = recent.AddMinutes(1)
            });

        db.SyncDeadLetters.AddRange(
            new SyncDeadLetter
            {
                Id = Guid.NewGuid(),
                OperationId = Guid.NewGuid(),
                DeviceId = "device-retention",
                UserId = "1",
                OperationType = "submit_answer",
                PayloadHash = "A",
                PayloadJson = "{}",
                RetryCount = 1,
                Status = SyncDeadLetterStatuses.Resolved,
                FailureReason = "Resolved failure",
                CreatedAtUtc = old,
                LastFailedAtUtc = old,
                ResolvedAtUtc = old
            },
            new SyncDeadLetter
            {
                Id = Guid.NewGuid(),
                OperationId = Guid.NewGuid(),
                DeviceId = "device-retention",
                UserId = "1",
                OperationType = "submit_answer",
                PayloadHash = "B",
                PayloadJson = "{}",
                RetryCount = 1,
                Status = SyncDeadLetterStatuses.Exhausted,
                FailureReason = "Exhausted failure",
                CreatedAtUtc = old.AddMinutes(1),
                LastFailedAtUtc = old.AddMinutes(1),
                ResolvedAtUtc = old.AddMinutes(1)
            },
            new SyncDeadLetter
            {
                Id = Guid.NewGuid(),
                OperationId = Guid.NewGuid(),
                DeviceId = "device-retention",
                UserId = "1",
                OperationType = "submit_answer",
                PayloadHash = "C",
                PayloadJson = "{}",
                RetryCount = 1,
                Status = SyncDeadLetterStatuses.Pending,
                FailureReason = "Pending failure",
                CreatedAtUtc = recent,
                LastFailedAtUtc = recent
            });
    }
}
