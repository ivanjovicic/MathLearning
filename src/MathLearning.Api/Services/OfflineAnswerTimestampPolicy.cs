using System.Globalization;
using MathLearning.Application.DTOs.Quiz;

namespace MathLearning.Api.Services;

public static class OfflineAnswerTimestampPolicy
{
    public static readonly TimeSpan MaxFutureSkew = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan MaxReplayAge = TimeSpan.FromDays(90);

    public static bool TryParseLegacy(
        string? answeredAtText,
        DateTime utcNow,
        int? questionId,
        out DateTime answeredAtUtc,
        out OfflineBatchSubmitIssue? issue)
    {
        if (string.IsNullOrWhiteSpace(answeredAtText))
        {
            answeredAtUtc = utcNow;
            issue = new OfflineBatchSubmitIssue(
                questionId,
                "answered_at_defaulted",
                "answeredAt missing; server UTC now used.");
            return true;
        }

        if (!DateTimeOffset.TryParse(
                answeredAtText,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            answeredAtUtc = default;
            issue = new OfflineBatchSubmitIssue(
                questionId,
                "invalid_timestamp",
                "answeredAt is malformed.");
            return false;
        }

        return TryNormalize(parsed.UtcDateTime, utcNow, questionId, out answeredAtUtc, out issue);
    }

    public static bool TryNormalize(
        DateTime answeredAt,
        DateTime utcNow,
        int? questionId,
        out DateTime answeredAtUtc,
        out OfflineBatchSubmitIssue? issue)
    {
        answeredAtUtc = NormalizeToUtcMilliseconds(answeredAt);

        if (answeredAtUtc > utcNow.Add(MaxFutureSkew))
        {
            issue = new OfflineBatchSubmitIssue(
                questionId,
                "timestamp_too_far_in_future",
                $"answeredAt exceeds allowed future skew of {MaxFutureSkew.TotalMinutes} minutes.");
            return false;
        }

        if (answeredAtUtc < utcNow.Subtract(MaxReplayAge))
        {
            issue = new OfflineBatchSubmitIssue(
                questionId,
                "timestamp_too_old",
                $"answeredAt is older than the {MaxReplayAge.TotalDays}-day offline replay window.");
            return false;
        }

        issue = null;
        return true;
    }

    public static DateTime NormalizeToUtcMilliseconds(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

        var ticks = utc.Ticks - (utc.Ticks % TimeSpan.TicksPerMillisecond);
        return new DateTime(ticks, DateTimeKind.Utc);
    }

    public static bool IsWithinAcceptedWindow(DateTime answeredAtUtc, DateTime utcNow) =>
        answeredAtUtc <= utcNow.Add(MaxFutureSkew) &&
        answeredAtUtc >= utcNow.Subtract(MaxReplayAge);
}
