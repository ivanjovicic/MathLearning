using System.Text.Json;

namespace MathLearning.Api.Services;

public interface IAdaptiveAnalyticsService
{
    void TrackEvent(string eventName, string userId, object? properties = null);
}

public sealed class AdaptiveAnalyticsService : IAdaptiveAnalyticsService
{
    private readonly ILogger<AdaptiveAnalyticsService> _logger;

    public AdaptiveAnalyticsService(ILogger<AdaptiveAnalyticsService> logger)
    {
        _logger = logger;
    }

    public void TrackEvent(string eventName, string userId, object? properties = null)
    {
        var serialized = properties is null
            ? "{}"
            : JsonSerializer.Serialize(properties);

        _logger.LogInformation(
            "AdaptiveAnalytics Event={EventName} UserId={UserId} Properties={PropertiesJson}",
            eventName,
            userId,
            serialized);
    }
}
