using MathLearning.Domain.Events;
using MathLearning.Domain.Primitives;

namespace MathLearning.Domain.Entities;

public class UserProgress : Entity
{
    public string UserId { get; private set; } = string.Empty;
    public int Coins { get; private set; }
    public int TotalXp { get; private set; }

    public UserStreak Streak { get; private set; } = default!;

    private UserProgress() { }

    public UserProgress(string userId)
    {
        UserId = userId;
        Streak = new UserStreak(userId);
    }

    public void ApplyQuizResult(int topicId, int correct, int total, int xp)
    {
        TotalXp += xp;
        Raise(new QuizCompleted(UserId, topicId, correct, total, xp));
    }

    public void GrantCoins(int amount, string reason)
    {
        Coins += amount;
        Raise(new CoinsGranted(UserId, amount, reason));
    }

    public void SpendCoins(int amount)
    {
        if (Coins < amount) throw new InvalidOperationException("Not enough coins.");
        Coins -= amount;
    }
}
