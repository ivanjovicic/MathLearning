using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Services;
using MathLearning.Infrastructure.Services.Leaderboard;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Tests.Services;

public class XpTrackingServiceTests
{
    [Fact]
    public async Task AddXpAsync_WritesEventAndRefreshesSchoolAggregates()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        db.Schools.Add(new School
        {
            Id = 10,
            Name = "Matematicka gimnazija",
            City = "Beograd",
            Country = "Serbia",
            CreatedAt = DateTime.UtcNow
        });

        var profile = await db.UserProfiles.FirstAsync(x => x.UserId == "1");
        profile.SchoolId = 10;
        profile.SchoolName = "Matematicka gimnazija";
        profile.LeaderboardOptIn = true;
        await db.SaveChangesAsync();

        var aggregationService = new SchoolLeaderboardAggregationService(db);
        var xpTrackingService = new XpTrackingService(db, aggregationService);

        await xpTrackingService.AddXpAsync("1", 25, "quiz_completion", "quiz-1");

        var xpEvent = await db.UserXpEvents.SingleAsync();
        Assert.Equal("1", xpEvent.UserId);
        Assert.Equal(25, xpEvent.XpDelta);
        Assert.Equal(25, xpEvent.ValidatedXpDelta);
        Assert.Equal("quiz_completion", xpEvent.SourceType);
        Assert.Equal("quiz-1", xpEvent.SourceId);
        Assert.Equal(10, xpEvent.SchoolId);

        var allTime = await db.SchoolScoreAggregates.SingleAsync(x => x.SchoolId == 10 && x.Period == "all_time");
        var weekly = await db.SchoolScoreAggregates.SingleAsync(x => x.SchoolId == 10 && x.Period == "week");
        var monthly = await db.SchoolScoreAggregates.SingleAsync(x => x.SchoolId == 10 && x.Period == "month");
        var daily = await db.SchoolScoreAggregates.SingleAsync(x => x.SchoolId == 10 && x.Period == "day");

        Assert.Equal(25, allTime.XpTotal);
        Assert.Equal(25, weekly.XpTotal);
        Assert.Equal(25, monthly.XpTotal);
        Assert.Equal(25, daily.XpTotal);
        Assert.Equal(1, weekly.ActiveStudents);
        Assert.Equal(1, weekly.EligibleStudents);
        Assert.Equal(1, weekly.Rank);
    }
}
