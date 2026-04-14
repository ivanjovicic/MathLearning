using Hangfire;
using MathLearning.Core.Services;
using Microsoft.Extensions.Logging;

namespace MathLearning.Infrastructure.Jobs;

public class LeaderboardJobs
{
    private readonly IRedisLeaderboardService _leaderboardService;
    private readonly ILogger<LeaderboardJobs> _logger;

    public LeaderboardJobs(IRedisLeaderboardService leaderboardService, ILogger<LeaderboardJobs> logger)
    {
        _leaderboardService = leaderboardService;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task DailyRankSnapshotJob()
    {
        _logger.LogInformation("Starting DailyRankSnapshotJob...");
        // Logic to snapshot daily ranks into SQL
        // Example: Fetch top ranks from Redis and store in SQL
        _logger.LogInformation("DailyRankSnapshotJob completed.");
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task WeeklyLeaderboardResetJob()
    {
        _logger.LogInformation("Starting WeeklyLeaderboardResetJob...");
        // Logic to reset weekly leaderboards in Redis
        // Example: Delete Redis keys for weekly leaderboards
        _logger.LogInformation("WeeklyLeaderboardResetJob completed.");
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task LeaderboardConsistencyJob()
    {
        _logger.LogInformation("Starting LeaderboardConsistencyJob...");
        // Logic to ensure Redis and SQL consistency
        // Example: Compare Redis and SQL data and fix discrepancies
        _logger.LogInformation("LeaderboardConsistencyJob completed.");
    }
}