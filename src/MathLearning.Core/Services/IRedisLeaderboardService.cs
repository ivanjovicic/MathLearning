namespace MathLearning.Core.Services;

using MathLearning.Core.DTOs;

public interface IRedisLeaderboardService
{
    Task<List<LeaderboardEntryDto>> GetLeaderboardAsync(LeaderboardRequestDto request);
    Task<LeaderboardEntryDto?> GetUserRankAsync(LeaderboardRequestDto request);
    Task<List<LeaderboardEntryDto>> GetNearRivalsAsync(LeaderboardRequestDto request);
    Task UpdateLeaderboardAsync(LeaderboardUpdateDto update);
}