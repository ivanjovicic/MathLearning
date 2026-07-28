namespace MathLearning.Api.Services;

public sealed class AdaptiveAnswerConflictException : Exception
{
    public AdaptiveAnswerConflictException(string message)
        : base(message)
    {
    }
}
