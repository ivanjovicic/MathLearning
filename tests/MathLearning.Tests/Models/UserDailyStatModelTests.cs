using MathLearning.Domain.Entities;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Tests.Models;

public class UserDailyStatModelTests
{
    [Fact]
    public async Task UserDailyStat_CanBeCreated()
    {
        var db = TestDbContextFactory.Create();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        db.UserDailyStats.Add(new UserDailyStat
        {
            UserId = 1,
            Day = today,
            Completed = false
        });
        await db.SaveChangesAsync();

        var stat = await db.UserDailyStats.FirstAsync(x => x.UserId == 1);
        Assert.Equal(today, stat.Day);
        Assert.False(stat.Completed);
    }

    [Fact]
    public async Task UserDailyStat_CanMarkCompleted()
    {
        var db = TestDbContextFactory.Create();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        db.UserDailyStats.Add(new UserDailyStat
        {
            UserId = 1,
            Day = today,
            Completed = false
        });
        await db.SaveChangesAsync();

        var stat = await db.UserDailyStats.FirstAsync(x => x.UserId == 1);
        stat.Completed = true;
        await db.SaveChangesAsync();

        var updated = await db.UserDailyStats.FirstAsync(x => x.UserId == 1);
        Assert.True(updated.Completed);
    }

    [Fact]
    public void UserDailyStat_DefaultValues()
    {
        var stat = new UserDailyStat();
        Assert.False(stat.Completed);
        Assert.NotEqual(Guid.Empty, stat.Id);
    }
}
