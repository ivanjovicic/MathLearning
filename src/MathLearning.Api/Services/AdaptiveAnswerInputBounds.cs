using MathLearning.Domain.Entities;

namespace MathLearning.Api.Services;

public static class AdaptiveAnswerInputBounds
{
    public const int MaxAnswerLength = 2000;
    public const int MaxResponseTimeSeconds = 3600;
    public const int MaxResponseTimeMilliseconds = MaxResponseTimeSeconds * 1000;

    public static bool TryValidateRequest(
        AdaptiveAnswerRequest request,
        DateTime utcNow,
        out string? error)
    {
        error = null;

        if (request.QuestionId <= 0)
        {
            error = "QuestionId must be a positive integer.";
            return false;
        }

        if (request.AdaptiveSessionId == Guid.Empty)
        {
            error = "AdaptiveSessionId is required.";
            return false;
        }

        if (request.AdaptiveSessionItemId == Guid.Empty)
        {
            error = "AdaptiveSessionItemId is required.";
            return false;
        }

        if (!TryValidateAnswer(request.Answer, out error))
            return false;

        if (!TryValidateConfidence(request.Confidence, out error))
            return false;

        if (!TryValidateResponseTimeSeconds(request.ResponseTimeSeconds, out error))
            return false;

        if (request.AnsweredAt is { } answeredAt
            && !TryValidateAnsweredAt(answeredAt, utcNow, out error))
        {
            return false;
        }

        return true;
    }

    public static bool TryValidateAnswer(string? answer, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(answer))
        {
            error = "Answer is required.";
            return false;
        }

        if (answer.Length > MaxAnswerLength)
        {
            error = $"Answer cannot exceed {MaxAnswerLength} characters.";
            return false;
        }

        return true;
    }

    public static bool TryValidateConfidence(double confidence, out string? error)
    {
        error = null;

        if (double.IsNaN(confidence) || double.IsInfinity(confidence))
        {
            error = "Confidence must be a finite number between 0 and 1.";
            return false;
        }

        if (confidence < 0d || confidence > 1d)
        {
            error = "Confidence must be between 0 and 1.";
            return false;
        }

        return true;
    }

    public static bool TryValidateResponseTimeSeconds(int responseTimeSeconds, out string? error)
    {
        error = null;

        if (responseTimeSeconds < 0)
        {
            error = "Response time must be non-negative.";
            return false;
        }

        if (responseTimeSeconds > MaxResponseTimeSeconds)
        {
            error = $"Response time cannot exceed {MaxResponseTimeSeconds} seconds.";
            return false;
        }

        return true;
    }

    public static bool TryValidateResponseTimeMilliseconds(int responseTimeMilliseconds, out string? error)
    {
        error = null;

        if (responseTimeMilliseconds < 0)
        {
            error = "Response time must be non-negative.";
            return false;
        }

        if (responseTimeMilliseconds > MaxResponseTimeMilliseconds)
        {
            error = $"Response time cannot exceed {MaxResponseTimeMilliseconds} milliseconds.";
            return false;
        }

        return true;
    }

    public static bool TryValidateAnsweredAt(DateTime answeredAt, DateTime utcNow, out string? error)
    {
        error = null;

        if (!OfflineAnswerTimestampPolicy.TryNormalize(
                answeredAt,
                utcNow,
                questionId: null,
                out _,
                out var issue))
        {
            error = issue?.Message ?? "answeredAt is outside the accepted time window.";
            return false;
        }

        return true;
    }
}
