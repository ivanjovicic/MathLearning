using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace MathLearning.Infrastructure.Services.EventBus;

public class OutboxProcessor : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(IServiceProvider sp, ILogger<OutboxProcessor> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("📬 OutboxProcessor started");

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
                    .Take(50)
                    .ToListAsync(ct);

                if (batch.Any())
                {
                    _logger.LogInformation("📤 Processing {Count} outbox messages", batch.Count);
                }

                foreach (var msg in batch)
                {
                    try
                    {
                        await bus.PublishAsync(msg.Type, msg.PayloadJson, ct);
                        msg.ProcessedUtc = DateTime.UtcNow;
                        msg.LastError = null;
                        _logger.LogDebug("✅ Processed outbox message {Id} of type {Type}", msg.Id, msg.Type);
                    }
                    catch (Exception ex)
                    {
                        msg.Attempts += 1;
                        msg.LastError = ex.Message;
                        _logger.LogError(ex, "❌ Failed to process outbox message {Id} (Attempt {Attempts})", msg.Id, msg.Attempts);
                        // opcionalno: dead-letter nakon N pokušaja
                    }
                }

                if (batch.Any())
                {
                    await db.SaveChangesAsync(ct);
                }

                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in OutboxProcessor main loop");
                // If the Outbox table doesn't exist, continuing to retry just spams logs.
                // The fix is to apply migrations that create the Outbox table and restart the app.
                if (ex is PostgresException pgEx && pgEx.SqlState == PostgresErrorCodes.UndefinedTable)
                {
                    _logger.LogWarning(pgEx, "Outbox processing disabled: missing database table. Apply migrations and restart.");
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }

        _logger.LogInformation("📬 OutboxProcessor stopped");
    }
}
