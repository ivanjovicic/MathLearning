using MathLearning.Application.DTOs.Leaderboard;
using System.Threading.Tasks;

namespace MathLearning.Application.Services
{
    public interface ILeaderboardService
    {
        Task<SchoolLeaderboardResponseDto> GetSchoolLeaderboardAsync(string userId, string period, int limit, string? cursor = null);
    }
}
