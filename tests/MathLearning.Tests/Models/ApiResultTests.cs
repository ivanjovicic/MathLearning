using System.Text.Json;
using MathLearning.Application.DTOs.Common;

namespace MathLearning.Tests.Models;

public sealed class ApiResultTests
{
    [Fact]
    public void Ok_SetsSuccessDataTraceAndHasNoError()
    {
        var result = ApiResult<string>.Ok("payload", traceId: "trace-ok");

        Assert.True(result.Success);
        Assert.False(result.HasError);
        Assert.Equal("payload", result.Data);
        Assert.Equal("trace-ok", result.TraceId);
        Assert.Null(result.Error);
        Assert.Null(result.ErrorCode);
        Assert.False(result.IsRateLimited);
        Assert.Null(result.RetryAfterSeconds);
    }

    [Fact]
    public void Fail_SetsAllErrorMetadataAndHasError()
    {
        var details = new { field = "questionId" };

        var result = ApiResult<int>.Fail(
            error: "Validation failed",
            errorCode: "VALIDATION_ERROR",
            errorDetails: details,
            traceId: "trace-fail",
            isRateLimited: true,
            retryAfterSeconds: 8);

        Assert.False(result.Success);
        Assert.True(result.HasError);
        Assert.Equal("Validation failed", result.Error);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Same(details, result.ErrorDetails);
        Assert.Equal("trace-fail", result.TraceId);
        Assert.True(result.IsRateLimited);
        Assert.Equal(8, result.RetryAfterSeconds);
        Assert.Equal(0, result.Data);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-retry-value")]
    public void ParseRetryAfterSeconds_WithMissingOrInvalidValue_ReturnsNull(string? value)
    {
        var seconds = RetryAfterParser.ParseRetryAfterSeconds(value);

        Assert.Null(seconds);
    }

    [Theory]
    [InlineData("120", 120)]
    [InlineData("0", 0)]
    [InlineData("-5", 0)]
    [InlineData("+7", 7)]
    public void ParseRetryAfterSeconds_WithIntegerValue_ReturnsNonNegativeSeconds(
        string value,
        int expected)
    {
        var seconds = RetryAfterParser.ParseRetryAfterSeconds(value);

        Assert.Equal(expected, seconds);
    }

    [Fact]
    public void ParseRetryAfterSeconds_WithFutureDate_RoundsUpFractionalSeconds()
    {
        var now = new DateTimeOffset(2026, 3, 5, 12, 0, 0, TimeSpan.Zero);
        var retryAt = now.AddMilliseconds(35_200).ToString("O");

        var seconds = RetryAfterParser.ParseRetryAfterSeconds(retryAt, now);

        Assert.Equal(36, seconds);
    }

    [Fact]
    public void ParseRetryAfterSeconds_WithPastDate_ReturnsZero()
    {
        var now = new DateTimeOffset(2026, 3, 5, 12, 0, 0, TimeSpan.Zero);
        var retryAt = now.AddMinutes(-2).ToString("R");

        var seconds = RetryAfterParser.ParseRetryAfterSeconds(retryAt, now);

        Assert.Equal(0, seconds);
    }

    [Fact]
    public void RateLimited_DefaultsMessageAndErrorCode()
    {
        var result = ApiResult<object>.RateLimited();

        Assert.False(result.Success);
        Assert.True(result.HasError);
        Assert.True(result.IsRateLimited);
        Assert.Equal("Too many requests.", result.Error);
        Assert.Equal("RATE_LIMITED", result.ErrorCode);
        Assert.Null(result.RetryAfterSeconds);
    }

    [Fact]
    public void RateLimitedResult_ContainsProvidedRateLimitMetadata()
    {
        var details = new { source = "test" };

        var result = ApiResult<object>.RateLimited(
            error: "Custom throttle message",
            errorDetails: details,
            traceId: "trace-1",
            retryAfterSeconds: 42);

        Assert.False(result.Success);
        Assert.True(result.IsRateLimited);
        Assert.Equal(42, result.RetryAfterSeconds);
        Assert.Equal("RATE_LIMITED", result.ErrorCode);
        Assert.Equal("Custom throttle message", result.Error);
        Assert.Same(details, result.ErrorDetails);
        Assert.Equal("trace-1", result.TraceId);
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
        Assert.True(parsed.HasError);
        Assert.Equal("VALIDATION_ERROR", parsed.ErrorCode);
        Assert.Equal("Validation failed", parsed.Error);
        Assert.Equal("trace-2", parsed.TraceId);
    }
}
