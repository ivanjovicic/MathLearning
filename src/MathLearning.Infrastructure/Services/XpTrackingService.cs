using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Infrastructure.Services;

/// <summary>
/// Helper service for updating user XP across all time periods
/// </summary>
public class XpTrackingService
{
    private readonly ApiDbContext _db;

    public XpTrackingService(ApiDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Adds XP to a user's profile across all time periods (total, daily, weekly, monthly)
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="xpAmount">Amount of XP to add</param>
    /// <returns>Updated user profile</returns>
    public async Task<UserProfile> AddXpAsync(string userId, int xpAmount)
    {
        var profile = await _db.UserProfiles.FindAsync(userId);
        if (profile == null)
            throw new InvalidOperationException($"User profile not found: {userId}");

        // Always add to total XP
        profile.Xp += xpAmount;

        // Initialize LastXpResetDate if not set
        if (profile.LastXpResetDate == null)
        {
            profile.LastXpResetDate = DateTime.UtcNow;
        }

        var now = DateTime.UtcNow;
        var lastReset = profile.LastXpResetDate.Value;

        // Check if we need to reset any counters before adding XP
        var today = now.Date;
        var lastResetDate = lastReset.Date;

        // Reset daily XP if it's a new day
        if (lastResetDate < today)
        {
            profile.DailyXp = 0;
        }

        // Reset weekly XP if it's a new week
        var weekStart = GetWeekStart(today);
        var lastWeekStart = GetWeekStart(lastResetDate);
        if (weekStart > lastWeekStart)
        {
            profile.WeeklyXp = 0;
        }

        // Reset monthly XP if it's a new month
        if (lastReset.Year < today.Year || lastReset.Month < today.Month)
        {
            profile.MonthlyXp = 0;
        }

        // Now add XP to all active periods
        profile.DailyXp += xpAmount;
        profile.WeeklyXp += xpAmount;
        profile.MonthlyXp += xpAmount;

        // Update reset timestamp
        profile.LastXpResetDate = now;

        // Update level based on total XP (every 100 XP = 1 level)
        profile.Level = 1 + (profile.Xp / 100);

        profile.UpdatedAt = now;

        await _db.SaveChangesAsync();

        return profile;
    }

    /// <summary>
    /// Gets the start of the week (Monday) for a given date
    /// </summary>
    private static DateTime GetWeekStart(DateTime date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        var daysToSubtract = dayOfWeek == 0 ? 6 : dayOfWeek - 1; // Sunday = 0, so we want to go back 6 days
        return date.AddDays(-daysToSubtract).Date;
    }

    /// <summary>
    /// Manually resets time-based XP for a specific user (admin function)
    /// </summary>
    public async Task ResetTimeBasedXpAsync(string userId)
    {
        var profile = await _db.UserProfiles.FindAsync(userId);
        if (profile == null)
            throw new InvalidOperationException($"User profile not found: {userId}");

        profile.DailyXp = 0;
        profile.WeeklyXp = 0;
        profile.MonthlyXp = 0;
        profile.LastXpResetDate = DateTime.UtcNow;
        profile.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }
}
