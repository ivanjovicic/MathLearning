using MathLearning.Infrastructure.Services;
using MathLearning.Application.DTOs.Leaderboard;

namespace MathLearning.Tests.Helpers;

using MathLearning.Application.Services;

public class MockLeaderboardService : ILeaderboardService
{
    public MockLeaderboardService()
    {
    }

    public Task<SchoolLeaderboardResponseDto> GetSchoolLeaderboardAsync(string userId, string period, int limit, string? cursor = null)
    {
        // Test mock: deterministic response
        var items = new List<SchoolLeaderboardItemDto>
        {
            new SchoolLeaderboardItemDto { Rank = 1, SchoolId = 1, SchoolName = "Test School A", Score = 3000, Members = 120 },
            new SchoolLeaderboardItemDto { Rank = 2, SchoolId = 2, SchoolName = "Test School B", Score = 2500, Members = 90 }
        };

        var resp = new SchoolLeaderboardResponseDto
        {
            Period = period,
            Items = items,
            MySchool = items.First(),
            NextCursor = null
        };

        return Task.FromResult(resp);
    }
}
