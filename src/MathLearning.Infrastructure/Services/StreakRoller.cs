using MathLearning.Domain.Entities;

namespace MathLearning.Infrastructure.Services;

public record StreakRollEvent(
    string Type,
    int MissedDays,
    int FreezesUsed,
    int StreakBefore,
    int StreakAfter
);

public static class StreakRoller
{
    public static StreakRollEvent? Apply(UserProfile profile, DateOnly today)
    {
        if (profile.Streak <= 0 || profile.LastStreakDay == null)
            return null;

        var last = profile.LastStreakDay.Value;
        var daysSince = today.DayNumber - last.DayNumber;

        if (daysSince <= 1)
            return null;

        var missedDays = daysSince - 1;
        var streakBefore = profile.Streak;
        var freezesUsed = 0;

        if (profile.StreakFreezeCount > 0)
        {
            freezesUsed = Math.Min(missedDays, profile.StreakFreezeCount);
            profile.StreakFreezeCount -= freezesUsed;
            missedDays -= freezesUsed;
        }

        if (missedDays <= 0)
        {
            // Preserve streak by advancing last streak day to yesterday.
            profile.LastStreakDay = today.AddDays(-1);
            profile.UpdatedAt = DateTime.UtcNow;
            return new StreakRollEvent(
                Type: "streak_freeze_used",
                MissedDays: daysSince - 1,
                FreezesUsed: freezesUsed,
                StreakBefore: streakBefore,
                StreakAfter: profile.Streak
            );
        }

        // Not enough freezes: reset streak.
        profile.Streak = 0;
        profile.LastStreakDay = null;
        profile.UpdatedAt = DateTime.UtcNow;

        return new StreakRollEvent(
            Type: "streak_reset",
            MissedDays: missedDays,
            FreezesUsed: freezesUsed,
            StreakBefore: streakBefore,
            StreakAfter: profile.Streak
        );
    }
}
