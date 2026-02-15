using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace MathLearning.Infrastructure.Services.EventBus;

public sealed class OutboxProcessor : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(5);

    private readonly IServiceProvider _sp;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(IServiceProvider sp, ILogger<OutboxProcessor> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("OutboxProcessor started");

        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var scope = _sp.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();

                    var batch = await db.Outbox
                        .Where(x => x.ProcessedUtc == null)
                        .OrderBy(x => x.OccurredUtc)
                        .Take(BatchSize)
                        .ToListAsync(ct);

                    if (batch.Count > 0)
                    {
                        _logger.LogInformation("Processing {Count} outbox messages", batch.Count);
                    }

                    foreach (var msg in batch)
                    {
                        if (ct.IsCancellationRequested)
                            break;

                        try
                        {
                            await bus.PublishAsync(msg.Type, msg.PayloadJson, ct);
                            msg.ProcessedUtc = DateTime.UtcNow;
                            msg.LastError = null;
                            _logger.LogDebug("Processed outbox message {Id} of type {Type}", msg.Id, msg.Type);
                        }
                        catch (OperationCanceledException) when (ct.IsCancellationRequested)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            msg.Attempts += 1;
                            msg.LastError = ex.Message;
                            _logger.LogError(ex, "Failed to process outbox message {Id} (Attempt {Attempts})", msg.Id, msg.Attempts);
                        }
                    }

                    if (batch.Count > 0)
                    {
                        await db.SaveChangesAsync(ct);
                    }

                    await DelaySafely(IdleDelay, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex) when (IsMissingOutboxTable(ex))
                {
                    // If the Outbox table doesn't exist, continuing to retry just spams logs.
                    // The fix is to apply migrations that create the Outbox table and restart the app.
                    _logger.LogWarning(ex, "Outbox processing disabled: missing database table. Apply migrations and restart.");
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in OutboxProcessor main loop");
                    await DelaySafely(ErrorDelay, ct);
                }
            }
        }
        finally
        {
            _logger.LogInformation("OutboxProcessor stopped");
        }
    }

    private static bool IsMissingOutboxTable(Exception ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
        {
            if (e is PostgresException pgEx && pgEx.SqlState == PostgresErrorCodes.UndefinedTable)
                return true;
        }

        return false;
    }

    private static async Task DelaySafely(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Normal shutdown
        }
    }
}

