using System.Text;
using MathLearning.Infrastructure.Services.Leaderboard;

namespace MathLearning.Tests.Services;

public sealed class LeaderboardCursorCodecTests
{
    [Fact]
    public void EncodeStudent_RoundTrips_WithScopeAndPeriodBinding()
    {
        var encoded = CursorCodec.EncodeStudent(123, "user-alpha", "global", "week");

        var decoded = CursorCodec.DecodeStudentOrThrow(encoded, "global", "week");

        Assert.NotNull(decoded);
        Assert.Equal(2, decoded!.V);
        Assert.Equal(123, decoded.Score);
        Assert.Equal("user-alpha", decoded.UserId);
        Assert.Equal("global", decoded.Scope);
        Assert.Equal("week", decoded.Period);
    }

    [Fact]
    public void DecodeStudentOrThrow_RejectsLegacyV1Cursor()
    {
        var legacy = Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"Score":123,"Id":42}"""));

        var exception = Assert.Throws<LeaderboardCursorException>(() =>
            CursorCodec.DecodeStudentOrThrow(legacy, "global", "week"));

        Assert.Equal("unsupported_cursor_version", exception.ErrorCode);
    }

    [Fact]
    public void DecodeStudentOrThrow_RejectsMismatchedScopeOrPeriod()
    {
        var encoded = CursorCodec.EncodeStudent(99, "user-alpha", "global", "week");

        var exception = Assert.Throws<LeaderboardCursorException>(() =>
            CursorCodec.DecodeStudentOrThrow(encoded, "friends", "week"));

        Assert.Equal("cursor_context_mismatch", exception.ErrorCode);
    }

    [Fact]
    public void DecodeStudentOrThrow_RejectsMissingRequiredFields()
    {
        var malformed = Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"V":2,"Score":123,"Scope":"global","Period":"week"}"""));

        var exception = Assert.Throws<LeaderboardCursorException>(() =>
            CursorCodec.DecodeStudentOrThrow(malformed, "global", "week"));

        Assert.Equal("invalid_cursor", exception.ErrorCode);
    }

    [Fact]
    public void DecodeStudentOrThrow_RejectsOversizedCursor()
    {
        var oversized = new string('a', 1025);

        var exception = Assert.Throws<LeaderboardCursorException>(() =>
            CursorCodec.DecodeStudentOrThrow(oversized, "global", "week"));

        Assert.Equal("cursor_too_large", exception.ErrorCode);
    }
}
