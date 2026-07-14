using System.Data;
using System.Text.RegularExpressions;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Persistance.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace MathLearning.Infrastructure.Services.EventBus;

public sealed class OutboxBatchProcessor
{
    private static readonly Regex ConnectionStringValueRegex = new(
        "(?i)(password|pwd|token|secret|apikey)\\s*=\\s*[^;\\s]+",
        RegexOptions.Compiled);

    private readonly AppDbContext db;
    private readonly IEventBus bus;
    private readonly ILogger<OutboxBatchProcessor> logger;
    private readonly OutboxProcessingOptions options;
    private readonly TimeProvider clock;

    public OutboxBatchProcessor(
        AppDbContext db,
        IEventBus bus,
        ILogger<OutboxBatchProcessor> logger,
        TimeProvider? clock = null,
        OutboxProcessingOptions? options = null)
    {
        this.db = db;
        this.bus = bus;
        this.logger = logger;
        this.clock = clock ?? TimeProvider.System;
        this.options = options ?? new OutboxProcessingOptions();
    }

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var now = UtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var claimedIds = await ClaimMessageIdsAsync(now, cancellationToken);
        if (claimedIds.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return 0;
        }

        var batch = await db.Outbox
            .Where(x => claimedIds.Contains(x.Id))
            .OrderBy(x => x.OccurredUtc)
            .ToListAsync(cancellationToken);

        foreach (var message in batch)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await bus.PublishAsync(message.Type, message.PayloadJson, cancellationToken);
                message.ProcessedUtc = UtcNow();
                message.NextAttemptUtc = null;
                message.DeadLetteredUtc = null;
                message.LastError = null;
                logger.LogDebug("Processed outbox message {Id} of type {Type}", message.Id, message.Type);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                var nextAttempt = message.Attempts + 1;
                var failedAt = UtcNow();

                message.Attempts = nextAttempt;
                message.LastError = SanitizePersistedError(ex, options.MaxPersistedErrorLength);
                message.NextAttemptUtc = nextAttempt >= options.MaxAttempts
                    ? null
                    : failedAt.Add(options.GetRetryDelay(nextAttempt));
                message.DeadLetteredUtc = nextAttempt >= options.MaxAttempts
                    ? failedAt
                    : null;

                logger.LogError(
                    ex,
                    "Failed to process outbox message {Id} (Attempt {Attempts}/{MaxAttempts})",
                    message.Id,
                    message.Attempts,
                    options.MaxAttempts);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return batch.Count;
    }

    internal static string SanitizePersistedError(Exception ex, int maxLength)
    {
        var message = ex.Message;
        message = ConnectionStringValueRegex.Replace(message, "$1=<redacted>");
        message = message.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();

        if (message.Length <= maxLength)
        {
            return message;
        }

        if (maxLength <= 3)
        {
            return message[..maxLength];
        }

        return $"{message[..(maxLength - 3)]}...";
    }

    private async Task<List<Guid>> ClaimMessageIdsAsync(DateTime now, CancellationToken cancellationToken)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);

        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText =
            """
            SELECT "Id"
            FROM "Outbox"
            WHERE "ProcessedUtc" IS NULL
              AND "DeadLetteredUtc" IS NULL
              AND COALESCE("NextAttemptUtc", '-infinity'::timestamp with time zone) <= @now
            ORDER BY "OccurredUtc"
            LIMIT @batchSize
            FOR UPDATE SKIP LOCKED;
            """;

        var nowParameter = command.CreateParameter();
        nowParameter.ParameterName = "now";
        nowParameter.Value = now;
        command.Parameters.Add(nowParameter);

        var batchParameter = command.CreateParameter();
        batchParameter.ParameterName = "batchSize";
        batchParameter.Value = options.BatchSize;
        command.Parameters.Add(batchParameter);

        var ids = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
    }

    private DateTime UtcNow() => clock.GetUtcNow().UtcDateTime;
}
