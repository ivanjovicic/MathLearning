using MathLearning.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace MathLearning.Tests.Services;

public sealed class RetryPolicyTests
{
    [Fact]
    public async Task ExecuteAsync_FirstAttemptSucceeds_ReturnsWithoutRetry()
    {
        var attempts = 0;

        var result = await RetryPolicy.ExecuteAsync(
            _ =>
            {
                attempts++;
                return Task.FromResult(42);
            },
            NullLogger.Instance,
            "success",
            CancellationToken.None,
            FastOptions(maxRetries: 3));

        Assert.Equal(42, result);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_TimeoutThenSuccess_RetriesOnce()
    {
        var attempts = 0;

        var result = await RetryPolicy.ExecuteAsync(
            _ =>
            {
                attempts++;
                return attempts == 1
                    ? Task.FromException<int>(new TimeoutException("transient"))
                    : Task.FromResult(7);
            },
            NullLogger.Instance,
            "timeout",
            CancellationToken.None,
            FastOptions(maxRetries: 1));

        Assert.Equal(7, result);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_TaskCanceledWithoutRequestCancellation_Retries()
    {
        var attempts = 0;

        var result = await RetryPolicy.ExecuteAsync(
            _ =>
            {
                attempts++;
                return attempts == 1
                    ? Task.FromException<string>(new TaskCanceledException("provider timeout"))
                    : Task.FromResult("ok");
            },
            NullLogger.Instance,
            "task-canceled",
            CancellationToken.None,
            FastOptions(maxRetries: 1));

        Assert.Equal("ok", result);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_DbUpdateException_RetriesWhenEnabled()
    {
        var attempts = 0;

        var result = await RetryPolicy.ExecuteAsync(
            _ =>
            {
                attempts++;
                return attempts == 1
                    ? Task.FromException<int>(new DbUpdateException("transient db failure"))
                    : Task.FromResult(9);
            },
            NullLogger.Instance,
            "db-enabled",
            CancellationToken.None,
            FastOptions(maxRetries: 1, retryTransientDbErrors: true));

        Assert.Equal(9, result);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_DbUpdateException_DoesNotRetryWhenDisabled()
    {
        var attempts = 0;

        await Assert.ThrowsAsync<DbUpdateException>(() => RetryPolicy.ExecuteAsync<int>(
            _ =>
            {
                attempts++;
                return Task.FromException<int>(new DbUpdateException("non-retryable db failure"));
            },
            NullLogger.Instance,
            "db-disabled",
            CancellationToken.None,
            FastOptions(maxRetries: 3, retryTransientDbErrors: false)));

        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_NonTransientException_DoesNotRetry()
    {
        var attempts = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() => RetryPolicy.ExecuteAsync<int>(
            _ =>
            {
                attempts++;
                return Task.FromException<int>(new InvalidOperationException("bad request"));
            },
            NullLogger.Instance,
            "non-transient",
            CancellationToken.None,
            FastOptions(maxRetries: 4)));

        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_ExhaustedRetries_ThrowsLastException()
    {
        var attempts = 0;

        await Assert.ThrowsAsync<TimeoutException>(() => RetryPolicy.ExecuteAsync<int>(
            _ =>
            {
                attempts++;
                return Task.FromException<int>(new TimeoutException($"attempt-{attempts}"));
            },
            NullLogger.Instance,
            "exhausted",
            CancellationToken.None,
            FastOptions(maxRetries: 2)));

        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_NegativeMaxRetries_IsTreatedAsZero()
    {
        var attempts = 0;

        await Assert.ThrowsAsync<TimeoutException>(() => RetryPolicy.ExecuteAsync<int>(
            _ =>
            {
                attempts++;
                return Task.FromException<int>(new TimeoutException("no retry"));
            },
            NullLogger.Instance,
            "negative-retries",
            CancellationToken.None,
            new RetryPolicyOptions(MaxRetries: -10, InitialDelayMs: 1)));

        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_CancelledBeforeStart_DoesNotInvokeOperation()
    {
        var attempts = 0;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RetryPolicy.ExecuteAsync(
            _ =>
            {
                attempts++;
                return Task.FromResult(1);
            },
            NullLogger.Instance,
            "cancelled",
            cancellation.Token,
            FastOptions(maxRetries: 2)));

        Assert.Equal(0, attempts);
    }

    [Theory]
    [InlineData("Retry-After")]
    [InlineData("retry-after")]
    public async Task ExecuteAsync_RetryAfterMetadata_IsParsedAndOperationRetries(string dataKey)
    {
        var attempts = 0;

        var result = await RetryPolicy.ExecuteAsync(
            _ =>
            {
                attempts++;
                if (attempts == 1)
                {
                    var exception = new TimeoutException("throttled");
                    exception.Data[dataKey] = "0";
                    return Task.FromException<int>(exception);
                }

                return Task.FromResult(5);
            },
            NullLogger.Instance,
            "retry-after",
            CancellationToken.None,
            FastOptions(maxRetries: 1));

        Assert.Equal(5, result);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public void RateLimitedOperationException_PreservesRetryAndInnerException()
    {
        var inner = new TimeoutException("provider timeout");

        var exception = new RateLimitedOperationException(
            "rate limited",
            retryAfterSeconds: 12,
            innerException: inner);

        Assert.Equal("rate limited", exception.Message);
        Assert.Equal(12, exception.RetryAfterSeconds);
        Assert.Same(inner, exception.InnerException);
    }

    private static RetryPolicyOptions FastOptions(
        int maxRetries,
        bool retryTransientDbErrors = true) =>
        new(
            MaxRetries: maxRetries,
            InitialDelayMs: 1,
            RetryTransientDbErrors: retryTransientDbErrors);
}
