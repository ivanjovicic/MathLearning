using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Serilog.Core;
using Serilog.Events;
using Serilog.Configuration;

namespace MathLearning.Api.Logging;

public class PostgreSqlSink : ILogEventSink
{
    private readonly IServiceProvider _serviceProvider;

    public PostgreSqlSink(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void Emit(LogEvent logEvent)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

            var log = new ApplicationLog
            {
                Timestamp = logEvent.Timestamp.UtcDateTime,
                Level = logEvent.Level.ToString(),
                Message = logEvent.RenderMessage(),
                Exception = logEvent.Exception?.ToString(),
                Properties = SerializeProperties(logEvent.Properties),
                RequestPath = GetPropertyValue(logEvent.Properties, "RequestPath"),
                UserName = GetPropertyValue(logEvent.Properties, "UserName"),
                MachineName = GetPropertyValue(logEvent.Properties, "MachineName")
            };

            dbContext.ApplicationLogs.Add(log);
            dbContext.SaveChanges();
        }
        catch
        {
            // Silently fail - don't break app if logging fails
        }
    }

    private string? SerializeProperties(IReadOnlyDictionary<string, LogEventPropertyValue> properties)
    {
        if (properties == null || properties.Count == 0)
            return null;

        var props = properties
            .Where(p => p.Key != "RequestPath" && p.Key != "UserName" && p.Key != "MachineName")
            .Select(p => $"{p.Key}: {p.Value}")
            .ToList();

        return props.Any() ? string.Join(", ", props) : null;
    }

    private string? GetPropertyValue(IReadOnlyDictionary<string, LogEventPropertyValue> properties, string key)
    {
        if (properties.TryGetValue(key, out var value))
        {
            return value.ToString().Trim('"');
        }
        return null;
    }
}

public static class PostgreSqlSinkExtensions
{
    public static Serilog.LoggerConfiguration PostgreSql(
        this LoggerSinkConfiguration loggerConfiguration,
        IServiceProvider serviceProvider)
    {
        return loggerConfiguration.Sink(new PostgreSqlSink(serviceProvider));
    }
}
