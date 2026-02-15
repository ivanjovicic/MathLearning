using MathLearning.Domain.Entities;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Tests.Models;

public class UserProfileModelTests
{
    [Fact]
    public async Task UserProfile_CanBeCreatedAndRetrieved()
    {
        var db = TestDbContextFactory.Create();

        db.Users.Add(new IdentityUser { Id = "99", UserName = "newuser", Email = "newuser@example.com" });
        db.UserProfiles.Add(new UserProfile
        {
            UserId = "99",
            Username = "newuser",
            DisplayName = "New User",
            Coins = 100,
            Level = 1,
            Xp = 0,
            Streak = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var found = await db.UserProfiles.FirstAsync(p => p.UserId == "99");
        Assert.Equal("newuser", found.Username);
        Assert.Equal(100, found.Coins);
    }

    [Fact]
    public void UserProfile_DefaultValues()
    {
        var profile = new UserProfile();

        Assert.Equal(100, profile.Coins);
        Assert.Equal(1, profile.Level);
        Assert.Equal(0, profile.Xp);
        Assert.Equal(0, profile.Streak);
        Assert.Equal(0, profile.TotalCoinsEarned);
        Assert.Equal(0, profile.TotalCoinsSpent);
    }

    [Fact]
    public async Task UserProfile_CoinsCanBeUpdated()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        var profile = await db.UserProfiles.FirstAsync(p => p.UserId == "1");
        profile.Coins -= 25;
        profile.TotalCoinsSpent += 25;
        await db.SaveChangesAsync();

        var updated = await db.UserProfiles.FirstAsync(p => p.UserId == "1");
        Assert.Equal(75, updated.Coins);
        Assert.Equal(25, updated.TotalCoinsSpent);
    }
}
