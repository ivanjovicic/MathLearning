using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace MathLearning.Infrastructure.Services.EventBus;

public sealed class OutboxProcessor : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly OutboxProcessingOptions _options;

    public OutboxProcessor(
        IServiceProvider sp,
        ILogger<OutboxProcessor> logger,
        OutboxProcessingOptions? options = null)
    {
        _sp = sp;
        _logger = logger;
        _options = options ?? new OutboxProcessingOptions();
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
                    var processor = scope.ServiceProvider.GetRequiredService<OutboxBatchProcessor>();
                    var processedCount = await processor.ProcessBatchAsync(ct);

                    if (processedCount > 0)
                    {
                        _logger.LogInformation("Processed {Count} outbox messages", processedCount);
                    }

                    await DelaySafely(_options.IdleDelay, ct);
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
                    await DelaySafely(_options.ErrorDelay, ct);
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

