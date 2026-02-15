using MathLearning.Domain.Entities;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Tests.Models;

public class UserSettingsModelTests
{
    [Fact]
    public async Task UserSettings_CanBeCreatedWithDefaults()
    {
        var db = TestDbContextFactory.Create();

        db.UserSettings.Add(new UserSettings
        {
            UserId = "1",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var settings = await db.UserSettings.FirstAsync(s => s.UserId == "1");

        Assert.Equal("sr", settings.Language);
        Assert.Equal("light", settings.Theme);
        Assert.True(settings.HintsEnabled);
        Assert.True(settings.SoundEnabled);
        Assert.True(settings.VibrationEnabled);
        Assert.False(settings.DailyNotificationEnabled);
        Assert.Equal("18:00", settings.DailyNotificationTime);
    }

    [Fact]
    public async Task UserSettings_CanBeUpdated()
    {
        var db = TestDbContextFactory.Create();

        db.UserSettings.Add(new UserSettings
        {
            UserId = "1",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var settings = await db.UserSettings.FirstAsync(s => s.UserId == "1");
        settings.Language = "en";
        settings.Theme = "dark";
        settings.HintsEnabled = false;
        settings.SoundEnabled = false;
        settings.DailyNotificationEnabled = true;
        settings.DailyNotificationTime = "09:00";
        settings.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var updated = await db.UserSettings.FirstAsync(s => s.UserId == "1");
        Assert.Equal("en", updated.Language);
        Assert.Equal("dark", updated.Theme);
        Assert.False(updated.HintsEnabled);
        Assert.False(updated.SoundEnabled);
        Assert.True(updated.DailyNotificationEnabled);
        Assert.Equal("09:00", updated.DailyNotificationTime);
    }

    [Fact]
    public void UserSettings_DefaultValues()
    {
        var settings = new UserSettings();

        Assert.Equal("sr", settings.Language);
        Assert.Equal("light", settings.Theme);
        Assert.True(settings.HintsEnabled);
        Assert.True(settings.SoundEnabled);
        Assert.True(settings.VibrationEnabled);
        Assert.False(settings.DailyNotificationEnabled);
        Assert.Equal("18:00", settings.DailyNotificationTime);
    }
}
