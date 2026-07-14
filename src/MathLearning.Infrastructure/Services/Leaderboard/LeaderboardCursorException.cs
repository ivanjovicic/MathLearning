namespace MathLearning.Infrastructure.Services.Leaderboard;

public sealed class LeaderboardCursorException : Exception
{
    public LeaderboardCursorException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}
