using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services;
using MathLearning.Infrastructure.Services.Leaderboard;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace MathLearning.Tests.Services;

public sealed class SchoolLeaderboardSnapshotIdempotencyTests
{
    [Fact]
    public async Task CaptureSnapshotAsync_DuplicateRunWithinTwentyMinutes_DoesNotDuplicateHistoryRows()
    {
        await using var db = TestDbContextFactory.Create();
        var periodInfo = SchoolLeaderboardPeriods.Normalize("week");

        db.Schools.Add(new School { Id = 1, Name = "Alpha School" });
        db.SchoolScoreAggregates.Add(new SchoolScoreAggregate
        {
            SchoolId = 1,
            Period = periodInfo.Period,
            PeriodStartUtc = periodInfo.PeriodStartUtc,
            XpTotal = 1200,
            ActiveStudents = 12,
            EligibleStudents = 20,
            ParticipationRate = 0.6m,
            CompositeScore = 95m,
            Rank = 1,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var sut = new LeaderboardService(db, NullLogger<LeaderboardService>.Instance);

        await sut.CaptureSnapshotAsync("week");
        var afterFirst = await db.SchoolRankHistories.CountAsync();

        await sut.CaptureSnapshotAsync("week");
        var afterSecond = await db.SchoolRankHistories.CountAsync();

        Assert.Equal(1, afterFirst);
        Assert.Equal(afterFirst, afterSecond);
    }
}
