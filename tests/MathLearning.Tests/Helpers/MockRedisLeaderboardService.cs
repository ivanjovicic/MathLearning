using MathLearning.Core.Services;
using MathLearning.Core.DTOs;

namespace MathLearning.Tests.Helpers;

public class MockRedisLeaderboardService : IRedisLeaderboardService
{
    private readonly List<LeaderboardEntryDto> _entries = new()
    {
        new LeaderboardEntryDto(1, "test-user", "Test User", 5, 1500, 200, 10),
        new LeaderboardEntryDto(2, "user_2", "User Two", 4, 1200, 150, 5),
        new LeaderboardEntryDto(3, "user_3", "User Three", 3, 900, 80, 2),
    };

    public Task<List<LeaderboardEntryDto>> GetLeaderboardAsync(LeaderboardRequestDto request)
    {
        return Task.FromResult(_entries.Take(request.Limit).ToList());
    }

    public Task<LeaderboardEntryDto?> GetUserRankAsync(LeaderboardRequestDto request)
    {
        var found = _entries.FirstOrDefault(e => e.UserId == (request.UserId ?? "test-user"));
        return Task.FromResult(found);
    }

    public Task<List<LeaderboardEntryDto>> GetNearRivalsAsync(LeaderboardRequestDto request)
    {
        return Task.FromResult(_entries.Take(5).ToList());
    }

    public Task UpdateLeaderboardAsync(LeaderboardUpdateDto update)
    {
        // no-op for tests
        return Task.CompletedTask;
    }
}
