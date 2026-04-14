using System.Text.Json;
using MathLearning.Application.DTOs.Common;

namespace MathLearning.Tests.Models;

public class ApiResultTests
{
    [Fact]
    public void ParseRetryAfterSeconds_WithIntegerValue_ReturnsSeconds()
    {
        var seconds = RetryAfterParser.ParseRetryAfterSeconds("120");
        Assert.Equal(120, seconds);
    }

    [Fact]
    public void ParseRetryAfterSeconds_WithDateValue_ReturnsNonNegativeSeconds()
    {
        var now = new DateTimeOffset(2026, 3, 5, 12, 0, 0, TimeSpan.Zero);
        var retryAt = now.AddSeconds(35).ToString("R");
        var seconds = RetryAfterParser.ParseRetryAfterSeconds(retryAt, now);

        Assert.NotNull(seconds);
        Assert.InRange(seconds!.Value, 34, 35);
    }

    [Fact]
    public void RateLimitedResult_ContainsRateLimitMetadata()
    {
        var result = ApiResult<object>.RateLimited(
            error: "Too many requests",
            errorDetails: new { source = "test" },
            traceId: "trace-1",
            retryAfterSeconds: 42);

        Assert.False(result.Success);
        Assert.True(result.IsRateLimited);
        Assert.Equal(42, result.RetryAfterSeconds);
        Assert.Equal("RATE_LIMITED", result.ErrorCode);
    }

    [Fact]
    public void JsonRoundTrip_PreservesErrorContract()
    {
        var original = ApiResult<string>.Fail(
            error: "Validation failed",
            errorCode: "VALIDATION_ERROR",
            errorDetails: new { field = "questionId" },
            traceId: "trace-2");

        var json = JsonSerializer.Serialize(original);
        var parsed = JsonSerializer.Deserialize<ApiResult<string>>(json);

        Assert.NotNull(parsed);
        Assert.False(parsed!.Success);
        Assert.Equal("VALIDATION_ERROR", parsed.ErrorCode);
        Assert.Equal("Validation failed", parsed.Error);
    }
}
