using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Services;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace MathLearning.Tests.Services;

public class XpTrackingServiceTests
{
    [Fact]
    public async Task AddXpAsync_WritesEventAndDefersSchoolAggregates()
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

        var xpTrackingService = new XpTrackingService(db, NullLogger<XpTrackingService>.Instance, null);

        await xpTrackingService.AddXpAsync("1", 25, "quiz_completion", "quiz-1");

        var xpEvent = await db.UserXpEvents.SingleAsync();
        Assert.Equal("1", xpEvent.UserId);
        Assert.Equal(25, xpEvent.XpDelta);
        Assert.Equal(25, xpEvent.ValidatedXpDelta);
        Assert.Equal("quiz_completion", xpEvent.SourceType);
        Assert.Equal("quiz-1", xpEvent.SourceId);
        Assert.Equal(10, xpEvent.SchoolId);

        Assert.Empty(await db.SchoolScoreAggregates.ToListAsync());
    }

    [Fact]
    public async Task AddXpAsync_DoesNotDuplicateWhenSourceIsReplayed()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var xpTrackingService = new XpTrackingService(db, NullLogger<XpTrackingService>.Instance, null);

        await xpTrackingService.AddXpAsync("1", 10, "sync_submit_answer", "op-1");
        await xpTrackingService.AddXpAsync("1", 10, "sync_submit_answer", "op-1");

        var profile = await db.UserProfiles.SingleAsync(x => x.UserId == "1");
        var xpEvents = await db.UserXpEvents.ToListAsync();

        Assert.Equal(10, profile.Xp);
        Assert.Single(xpEvents);
        Assert.Equal("op-1", xpEvents[0].SourceId);
    }
}
