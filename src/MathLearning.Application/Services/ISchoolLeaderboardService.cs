using MathLearning.Application.DTOs.Leaderboard;

namespace MathLearning.Application.Services;

public interface ISchoolLeaderboardService
{
    Task EnsureCurrentPeriodAsync(string period, CancellationToken ct = default);
    Task RefreshCurrentPeriodAsync(string period, CancellationToken ct = default);
    Task RefreshAllCurrentPeriodsAsync(CancellationToken ct = default);
    Task CaptureSnapshotAsync(string period, CancellationToken ct = default);
    Task<SchoolLeaderboardDetailDto?> GetSchoolLeaderboardDetailsAsync(int schoolId, string period, int neighbors = 2, CancellationToken ct = default);
    Task<SchoolLeaderboardHistoryResponseDto> GetSchoolLeaderboardHistoryAsync(int schoolId, string period, int take = 30, CancellationToken ct = default);
}
