using MathLearning.Domain.Events;
using MathLearning.Domain.Primitives;

namespace MathLearning.Domain.Entities;

public class UserStreak : Entity
{
    public int UserId { get; private set; }
    public int CurrentStreak { get; private set; }
    public DateOnly LastActivityDate { get; private set; }
    public int FreezeCount { get; private set; }

    private UserStreak() { }

    public UserStreak(int userId)
    {
        UserId = userId;
        CurrentStreak = 0;
        LastActivityDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        FreezeCount = 0;
    }

    public void AddFreeze(int qty, int max = 2)
        => FreezeCount = Math.Min(max, FreezeCount + qty);

    public void RegisterActivity(DateOnly today)
    {
        if (LastActivityDate == today) return;

        if (LastActivityDate == today.AddDays(-1))
            CurrentStreak += 1;
        else
            CurrentStreak = 1;

        LastActivityDate = today;
        // (po želji Raise StreakUpdated event)
    }

    public void ValidateOnLogin(DateOnly today)
    {
        var missedDays = today.DayNumber - LastActivityDate.DayNumber;

        if (missedDays <= 1) return;

        if (FreezeCount > 0)
        {
            FreezeCount -= 1;
            LastActivityDate = today; // "zamrzni" gap
            Raise(new StreakProtectedByFreeze(UserId, today.AddDays(-1), FreezeCount));
        }
        else
        {
            CurrentStreak = 0;
            // Raise(new StreakLost(...)) ako želiš
        }
    }
}
