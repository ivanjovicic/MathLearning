using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MathLearning.Application.DTOs.Progress
{
    public record LeaderboardEntryDto(
    int Rank,
    int UserId,
    string DisplayName,
    int Level,
    int Xp,
    int WeeklyXp,
    int Streak
);

}
