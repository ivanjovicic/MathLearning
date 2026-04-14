using MathLearning.Application.DTOs.Leaderboard;

namespace MathLearning.Application.Services;

/// <summary>
/// Student leaderboard read path.
/// Decoupled from school leaderboard operations.
/// </summary>
public interface IStudentLeaderboardService
{
    Task<LeaderboardResponseDto> GetLeaderboardAsync(
        string userId,
        string scope,
        string period,
        int limit,
        string? cursor = null,
        bool includeMe = true,
        CancellationToken ct = default);
}
