using MathLearning.Api.Services;

namespace MathLearning.Tests.Services;

public sealed class OfflineAnswerTimestampPolicyTests
{
  [Fact]
  public void TryParseLegacy_LocalOffset_NormalizesToUtc()
  {
    var utcNow = new DateTime(2026, 6, 24, 12, 0, 0, DateTimeKind.Utc);

    Assert.True(OfflineAnswerTimestampPolicy.TryParseLegacy(
        "2026-06-24T12:00:00+02:00",
        utcNow,
        questionId: 1,
        out var answeredAtUtc,
        out var issue));

    Assert.Null(issue);
    Assert.Equal(new DateTime(2026, 6, 24, 10, 0, 0, DateTimeKind.Utc), answeredAtUtc);
  }

  [Fact]
  public void NormalizeToUtcMilliseconds_TruncatesSubMillisecondPrecision()
  {
    var value = new DateTime(2026, 6, 24, 10, 0, 0, 456, DateTimeKind.Utc).AddTicks(1234);
    var normalized = OfflineAnswerTimestampPolicy.NormalizeToUtcMilliseconds(value);

    Assert.Equal(new DateTime(2026, 6, 24, 10, 0, 0, 456, DateTimeKind.Utc), normalized);
  }

  [Fact]
  public void TryParseLegacy_MalformedTimestamp_ReturnsDiagnostic()
  {
    Assert.False(OfflineAnswerTimestampPolicy.TryParseLegacy(
        "not-a-timestamp",
        DateTime.UtcNow,
        questionId: 7,
        out _,
        out var issue));

    Assert.Equal("invalid_timestamp", issue!.Code);
    Assert.Equal(7, issue.QuestionId);
  }

  [Fact]
  public void TryNormalize_FutureAndVeryOldTimestamps_AreRejected()
  {
    var utcNow = new DateTime(2026, 6, 24, 12, 0, 0, DateTimeKind.Utc);

    Assert.False(OfflineAnswerTimestampPolicy.TryNormalize(
        utcNow.AddHours(1),
        utcNow,
        questionId: 2,
        out _,
        out var futureIssue));
    Assert.Equal("timestamp_too_far_in_future", futureIssue!.Code);

    Assert.False(OfflineAnswerTimestampPolicy.TryNormalize(
        utcNow.AddDays(-91),
        utcNow,
        questionId: 3,
        out _,
        out var oldIssue));
    Assert.Equal("timestamp_too_old", oldIssue!.Code);
  }
}
