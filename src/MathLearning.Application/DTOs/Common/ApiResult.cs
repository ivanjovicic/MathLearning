using System.Globalization;
using System.Text.Json.Serialization;

namespace MathLearning.Application.DTOs.Common;

public sealed class ApiResult<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? Error { get; init; }
    public string? ErrorCode { get; init; }
    public object? ErrorDetails { get; init; }
    public bool IsRateLimited { get; init; }
    public int? RetryAfterSeconds { get; init; }
    public string? TraceId { get; init; }
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    [JsonIgnore]
    public bool HasError => !Success;

    public static ApiResult<T> Ok(T data, string? traceId = null) =>
        new()
        {
            Success = true,
            Data = data,
            TraceId = traceId
        };

    public static ApiResult<T> Fail(
        string error,
        string? errorCode = null,
        object? errorDetails = null,
        string? traceId = null,
        bool isRateLimited = false,
        int? retryAfterSeconds = null) =>
        new()
        {
            Success = false,
            Error = error,
            ErrorCode = errorCode,
            ErrorDetails = errorDetails,
            IsRateLimited = isRateLimited,
            RetryAfterSeconds = retryAfterSeconds,
            TraceId = traceId
        };

    public static ApiResult<T> RateLimited(
        string? error = null,
        object? errorDetails = null,
        string? traceId = null,
        int? retryAfterSeconds = null) =>
        Fail(
            error ?? "Too many requests.",
            errorCode: "RATE_LIMITED",
            errorDetails: errorDetails,
            traceId: traceId,
            isRateLimited: true,
            retryAfterSeconds: retryAfterSeconds);
}

public static class RetryAfterParser
{
    public static int? ParseRetryAfterSeconds(string? retryAfterValue, DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(retryAfterValue))
            return null;

        if (int.TryParse(retryAfterValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
            return Math.Max(0, seconds);

        if (DateTimeOffset.TryParse(retryAfterValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var retryAt))
        {
            var reference = now ?? DateTimeOffset.UtcNow;
            var diff = retryAt - reference;
            return Math.Max(0, (int)Math.Ceiling(diff.TotalSeconds));
        }

        return null;
    }
}
