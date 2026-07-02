using MathLearning.Api.Services;
using MathLearning.Domain.Entities;

namespace MathLearning.Tests.Services;

public sealed class AdaptiveAnswerInputBoundsTests
{
    private static readonly DateTime UtcNow = new(2026, 6, 24, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(-1d)]
    [InlineData(2d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(999d)]
    public void TryValidateConfidence_OutOfRange_ReturnsFalse(double confidence)
    {
        var valid = AdaptiveAnswerInputBounds.TryValidateConfidence(confidence, out var error);

        Assert.False(valid);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(0.5d)]
    [InlineData(1d)]
    public void TryValidateConfidence_InRange_ReturnsTrue(double confidence)
    {
        var valid = AdaptiveAnswerInputBounds.TryValidateConfidence(confidence, out var error);

        Assert.True(valid);
        Assert.Null(error);
    }

    [Fact]
    public void TryValidateResponseTimeSeconds_HugeValue_ReturnsFalse()
    {
        var valid = AdaptiveAnswerInputBounds.TryValidateResponseTimeSeconds(
            AdaptiveAnswerInputBounds.MaxResponseTimeSeconds + 1,
            out var error);

        Assert.False(valid);
        Assert.Contains("seconds", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryValidateResponseTimeMilliseconds_HugeValue_ReturnsFalse()
    {
        var valid = AdaptiveAnswerInputBounds.TryValidateResponseTimeMilliseconds(
            AdaptiveAnswerInputBounds.MaxResponseTimeMilliseconds + 1,
            out var error);

        Assert.False(valid);
        Assert.Contains("milliseconds", error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-500)]
    public void TryValidateResponseTimeSeconds_Negative_ReturnsFalse(int responseTimeSeconds)
    {
        var valid = AdaptiveAnswerInputBounds.TryValidateResponseTimeSeconds(responseTimeSeconds, out var error);

        Assert.False(valid);
        Assert.Contains("non-negative", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryValidateAnsweredAt_FutureTimestamp_ReturnsFalse()
    {
        var future = UtcNow.Add(OfflineAnswerTimestampPolicy.MaxFutureSkew).AddMinutes(5);

        var valid = AdaptiveAnswerInputBounds.TryValidateAnsweredAt(future, UtcNow, out var error);

        Assert.False(valid);
        Assert.Contains("future", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryValidateAnsweredAt_TooOldTimestamp_ReturnsFalse()
    {
        var tooOld = UtcNow.Subtract(OfflineAnswerTimestampPolicy.MaxReplayAge).AddDays(-1);

        var valid = AdaptiveAnswerInputBounds.TryValidateAnsweredAt(tooOld, UtcNow, out var error);

        Assert.False(valid);
        Assert.Contains("older", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryValidateAnswer_Oversized_ReturnsFalse()
    {
        var answer = new string('a', AdaptiveAnswerInputBounds.MaxAnswerLength + 1);

        var valid = AdaptiveAnswerInputBounds.TryValidateAnswer(answer, out var error);

        Assert.False(valid);
        Assert.Contains("cannot exceed", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryValidateRequest_InvalidConfidence_ReturnsFalse()
    {
        var request = CreateValidRequest();
        request.Confidence = 2d;

        var valid = AdaptiveAnswerInputBounds.TryValidateRequest(request, UtcNow, out var error);

        Assert.False(valid);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    private static AdaptiveAnswerRequest CreateValidRequest() =>
        new()
        {
            AdaptiveSessionId = Guid.NewGuid(),
            AdaptiveSessionItemId = Guid.NewGuid(),
            QuestionId = 1,
            Answer = "42",
            ResponseTimeSeconds = 12,
            Confidence = 0.75d,
            AnsweredAt = UtcNow.AddMinutes(-1)
        };
}
