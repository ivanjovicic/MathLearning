using Hangfire;
using MathLearning.Application.Services;
using Npgsql;

namespace MathLearning.Api.Services;

public interface ISchoolLeaderboardHangfireJobs
{
    Task RefreshAllCurrentPeriodsJob();
    Task CaptureSnapshotJob(string period);
}

public sealed class SchoolLeaderboardHangfireJobs : ISchoolLeaderboardHangfireJobs
{
    private readonly ISchoolLeaderboardService _schoolLeaderboardService;
    private readonly ILogger<SchoolLeaderboardHangfireJobs> _logger;

    public SchoolLeaderboardHangfireJobs(
        ISchoolLeaderboardService schoolLeaderboardService,
        ILogger<SchoolLeaderboardHangfireJobs> logger)
    {
        _schoolLeaderboardService = schoolLeaderboardService;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task RefreshAllCurrentPeriodsJob()
    {
        _logger.LogInformation("Refreshing school leaderboard aggregates for all active periods.");
        try
        {
            await _schoolLeaderboardService.RefreshAllCurrentPeriodsAsync();
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedColumn)
        {
            _logger.LogWarning(
                ex,
                "Skipping school leaderboard refresh because the database schema is behind the current model. Apply pending migrations and restart the API.");
        }
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task CaptureSnapshotJob(string period)
    {
        _logger.LogInformation("Capturing school leaderboard snapshot for period {Period}.", period);
        try
        {
            await _schoolLeaderboardService.CaptureSnapshotAsync(period);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedColumn)
        {
            _logger.LogWarning(
                ex,
                "Skipping school leaderboard snapshot for period {Period} because the database schema is behind the current model.",
                period);
        }
    }
}
