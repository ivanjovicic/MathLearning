using MathLearning.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace MathLearning.Tests.Services;

public class RetryPolicyTests
{
    [Fact]
    public async Task ExecuteAsync_RetriesTransientFailure_ThenReturnsResult()
    {
        var attempts = 0;
        var result = await RetryPolicy.ExecuteAsync(
            operation: _ =>
            {
                attempts++;
                if (attempts == 1)
                    throw new TimeoutException("Transient failure");

                return Task.FromResult("ok");
            },
            logger: NullLogger.Instance,
            operationName: "retry_test",
            cancellationToken: CancellationToken.None,
            options: new RetryPolicyOptions(MaxRetries: 2, InitialDelayMs: 1));

        Assert.Equal("ok", result);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoRetries_ThrowsImmediately()
    {
        await Assert.ThrowsAsync<TimeoutException>(() =>
            RetryPolicy.ExecuteAsync<string>(
                operation: _ => Task.FromException<string>(new TimeoutException("fail")),
                logger: NullLogger.Instance,
                operationName: "no_retry_test",
                cancellationToken: CancellationToken.None,
                options: new RetryPolicyOptions(MaxRetries: 0, InitialDelayMs: 1)));
    }
}
