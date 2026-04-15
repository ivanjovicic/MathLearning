using MathLearning.Domain.Entities;
<<<<<<< HEAD
=======
using MathLearning.Infrastructure.Persistance;
>>>>>>> b6bd21f (feat: harden XP audit pipeline and transactional quiz processing)
using MathLearning.Infrastructure.Services;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
<<<<<<< HEAD
=======
using Microsoft.Extensions.Options;
>>>>>>> b6bd21f (feat: harden XP audit pipeline and transactional quiz processing)

namespace MathLearning.Tests.Services;

public class XpTrackingServiceTests
{
    [Fact]
<<<<<<< HEAD
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
=======
    public async Task FirstCorrectAnswer_AwardsXp()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var service = CreateService(db, new XpTrackingOptions
        {
            EnableXpCaps = true,
            DailyXpCap = 500,
            WeeklyXpCap = 2000,
            MonthlyXpCap = 6000
        });

        var result = await AwardCorrectAttemptAsync(db, service, "1", 1);

        Assert.True(result.IsFirstTimeCorrect);
        Assert.Equal(10, result.AwardedXp);
        Assert.Equal(10, result.TotalXp);
    }

    [Fact]
    public async Task RepeatedCorrectAnswer_DoesNotAwardXp()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var service = CreateService(db, new XpTrackingOptions());

        var first = await AwardCorrectAttemptAsync(db, service, "1", 1);
        var second = await AwardCorrectAttemptAsync(db, service, "1", 1);

        Assert.True(first.IsFirstTimeCorrect);
        Assert.False(second.IsFirstTimeCorrect);
        Assert.Equal(10, first.AwardedXp);
        Assert.Equal(0, second.AwardedXp);
        Assert.Equal(10, second.TotalXp);
    }

    [Fact]
    public async Task DailyCapReached_RejectsXp()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var profile = await db.UserProfiles.FirstAsync(p => p.UserId == "1");
        profile.DailyXp = 10;
        profile.WeeklyXp = 10;
        profile.MonthlyXp = 10;
        profile.LastXpResetDate = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var service = CreateService(db, new XpTrackingOptions
        {
            EnableXpCaps = true,
            DailyXpCap = 10,
            WeeklyXpCap = 1000,
            MonthlyXpCap = 1000
        });

        var result = await AwardCorrectAttemptAsync(db, service, "1", 1);

        Assert.True(result.IsFirstTimeCorrect);
        Assert.Equal(0, result.AwardedXp);
        Assert.Equal(0, await db.UserAnswerAudits.CountAsync(a => a.AwardedXp > 0));
    }

    private static XpTrackingService CreateService(ApiDbContext db, XpTrackingOptions options)
    {
        return new XpTrackingService(
            db,
            Options.Create(options),
            NullLogger<XpTrackingService>.Instance);
    }

    private static async Task<(bool IsFirstTimeCorrect, int AwardedXp, int TotalXp)> AwardCorrectAttemptAsync(
        ApiDbContext db,
        XpTrackingService service,
        string userId,
        int questionId)
    {
        var stat = await db.UserQuestionStats
            .FirstOrDefaultAsync(s => s.UserId == userId && s.QuestionId == questionId);
        if (stat == null)
        {
            stat = new UserQuestionStat
            {
                UserId = userId,
                QuestionId = questionId
            };
            db.UserQuestionStats.Add(stat);
        }

        var isFirstTimeCorrect = stat.CorrectAttempts == 0;
        stat.Attempts++;
        stat.CorrectAttempts++;
        stat.LastAttemptAt = DateTime.UtcNow;

        var award = isFirstTimeCorrect
            ? await service.AddXpWithinTransactionAsync(userId, 10, false, "unit_test", db)
            : new XpAwardResult(0, (await db.UserProfiles.FirstAsync(p => p.UserId == userId)).Xp, "already_awarded", 0);

        db.UserAnswerAudits.Add(new UserAnswerAudit
        {
            UserId = userId,
            QuestionId = questionId,
            Source = "unit_test",
            IsOffline = false,
            Answer = "2",
            IsCorrect = true,
            IsFirstTimeCorrect = isFirstTimeCorrect,
            AwardedXp = award.AwardedXp,
            TotalXpAfterAward = award.TotalXpAfterAward,
            Reason = award.Reason,
            AnsweredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        return (isFirstTimeCorrect, award.AwardedXp, award.TotalXpAfterAward);
>>>>>>> b6bd21f (feat: harden XP audit pipeline and transactional quiz processing)
    }
}
