using MathLearning.Infrastructure.Services;
using MathLearning.Application.DTOs.Leaderboard;

namespace MathLearning.Tests.Helpers;

using MathLearning.Application.Services;

public class MockLeaderboardService : ILeaderboardService, ISchoolLeaderboardService
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

    public Task EnsureCurrentPeriodAsync(string period, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task RefreshCurrentPeriodAsync(string period, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task RefreshAllCurrentPeriodsAsync(CancellationToken ct = default)
        => Task.CompletedTask;

    public Task CaptureSnapshotAsync(string period, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<SchoolLeaderboardDetailDto?> GetSchoolLeaderboardDetailsAsync(int schoolId, string period, int neighbors = 2, CancellationToken ct = default)
    {
        var school = new SchoolLeaderboardItemDto
        {
            Rank = 1,
            SchoolId = schoolId,
            SchoolName = "Test School A",
            Score = 3000,
            Members = 120,
            RankingScore = 0.91m,
            ActiveStudents = 90,
            EligibleStudents = 120,
            ParticipationRate = 0.75m,
            AverageXpPerActiveStudent = 33.33m
        };

        var detail = new SchoolLeaderboardDetailDto
        {
            Period = period,
            School = school,
            NearbySchools =
            [
                new SchoolLeaderboardItemDto
                {
                    Rank = 2,
                    SchoolId = 2,
                    SchoolName = "Test School B",
                    Score = 2500,
                    Members = 90,
                    RankingScore = 0.83m,
                    ActiveStudents = 70,
                    EligibleStudents = 90,
                    ParticipationRate = 0.78m,
                    AverageXpPerActiveStudent = 35.71m
                }
            ]
        };

        return Task.FromResult<SchoolLeaderboardDetailDto?>(detail);
    }

    public Task<SchoolLeaderboardHistoryResponseDto> GetSchoolLeaderboardHistoryAsync(int schoolId, string period, int take = 30, CancellationToken ct = default)
    {
        var response = new SchoolLeaderboardHistoryResponseDto
        {
            SchoolId = schoolId,
            Period = period,
            Points =
            [
                new SchoolLeaderboardHistoryPointDto
                {
                    SnapshotTimeUtc = DateTime.UtcNow.AddHours(-1),
                    Rank = 2,
                    Score = 2600,
                    ActiveStudents = 80,
                    ParticipationRate = 0.70m,
                    CompositeScore = 0.82m
                },
                new SchoolLeaderboardHistoryPointDto
                {
                    SnapshotTimeUtc = DateTime.UtcNow,
                    Rank = 1,
                    Score = 3000,
                    ActiveStudents = 90,
                    ParticipationRate = 0.75m,
                    CompositeScore = 0.91m
                }
            ]
        };

        return Task.FromResult(response);
    }
}
