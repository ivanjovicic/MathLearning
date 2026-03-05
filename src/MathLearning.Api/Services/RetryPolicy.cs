using MathLearning.Application.DTOs.Common;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Api.Services;

public sealed class RateLimitedOperationException : Exception
{
    public RateLimitedOperationException(string message, int? retryAfterSeconds = null, Exception? innerException = null)
        : base(message, innerException)
    {
        RetryAfterSeconds = retryAfterSeconds;
    }

    public int? RetryAfterSeconds { get; }
}

public sealed record RetryPolicyOptions(
    int MaxRetries = 2,
    int InitialDelayMs = 250,
    bool RetryTransientDbErrors = true);

public static class RetryPolicy
{
    public static async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        ILogger logger,
        string operationName,
        CancellationToken cancellationToken,
        RetryPolicyOptions? options = null)
    {
        options ??= new RetryPolicyOptions();
        var maxRetries = Math.Max(0, options.MaxRetries);

        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await operation(cancellationToken);
            }
            catch (Exception ex) when (attempt < maxRetries && IsRetryable(ex, options))
            {
                var retryAfterSeconds = ResolveRetryAfterSeconds(ex);
                var delay = BuildDelay(attempt, options.InitialDelayMs, retryAfterSeconds);

                logger.LogWarning(
                    ex,
                    "Retrying operation {OperationName}. Attempt={Attempt}/{MaxAttempts} DelayMs={DelayMs} RetryAfterSeconds={RetryAfterSeconds}",
                    operationName,
                    attempt + 1,
                    maxRetries,
                    (int)delay.TotalMilliseconds,
                    retryAfterSeconds);

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private static bool IsRetryable(Exception ex, RetryPolicyOptions options)
    {
        if (ex is RateLimitedOperationException)
            return true;

        if (options.RetryTransientDbErrors && ex is DbUpdateException)
            return true;

        return ex is TimeoutException || ex is TaskCanceledException;
    }

    private static int? ResolveRetryAfterSeconds(Exception ex)
    {
        if (ex is RateLimitedOperationException rateLimited)
            return rateLimited.RetryAfterSeconds;

        if (ex.Data.Contains("Retry-After"))
            return RetryAfterParser.ParseRetryAfterSeconds(ex.Data["Retry-After"]?.ToString());

        if (ex.Data.Contains("retry-after"))
            return RetryAfterParser.ParseRetryAfterSeconds(ex.Data["retry-after"]?.ToString());

        return null;
    }

    private static TimeSpan BuildDelay(int attempt, int initialDelayMs, int? retryAfterSeconds)
    {
        if (retryAfterSeconds is > 0)
            return TimeSpan.FromSeconds(retryAfterSeconds.Value);

        var cappedInitial = Math.Max(50, initialDelayMs);
        var exponential = cappedInitial * Math.Pow(2, attempt);
        var jitter = Random.Shared.Next(50, 150);
        var delayMs = Math.Min(5_000, exponential + jitter);
        return TimeSpan.FromMilliseconds(delayMs);
    }
}
