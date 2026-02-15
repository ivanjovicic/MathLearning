using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Api.Services;

/// <summary>
/// Background service that periodically resets time-based XP counters (daily, weekly, monthly)
/// </summary>
public class XpResetBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<XpResetBackgroundService> _logger;

    public XpResetBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<XpResetBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🔄 XP Reset Background Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ResetExpiredXpCounters();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error resetting XP counters");
            }

            // Check every hour
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task ResetExpiredXpCounters()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        var now = DateTime.UtcNow;
        var today = now.Date;

        // Get all profiles that need XP reset
        var profiles = await db.UserProfiles
            .Where(p => p.LastXpResetDate == null || p.LastXpResetDate < today)
            .ToListAsync();

        if (!profiles.Any())
            return;

        var dailyResets = 0;
        var weeklyResets = 0;
        var monthlyResets = 0;

        foreach (var profile in profiles)
        {
            var lastReset = profile.LastXpResetDate ?? DateTime.MinValue;

            // Reset daily XP if it's a new day
            if (lastReset.Date < today)
            {
                profile.DailyXp = 0;
                dailyResets++;
            }

            // Reset weekly XP if it's a new week (Monday)
            var weekStart = GetWeekStart(today);
            var lastWeekStart = GetWeekStart(lastReset.Date);
            if (weekStart > lastWeekStart)
            {
                profile.WeeklyXp = 0;
                weeklyResets++;
            }

            // Reset monthly XP if it's a new month
            if (lastReset.Year < today.Year || lastReset.Month < today.Month)
            {
                profile.MonthlyXp = 0;
                monthlyResets++;
            }

            profile.LastXpResetDate = now;
        }

        await db.SaveChangesAsync();

        if (dailyResets > 0 || weeklyResets > 0 || monthlyResets > 0)
        {
            _logger.LogInformation(
                "✅ XP reset completed: {DailyResets} daily, {WeeklyResets} weekly, {MonthlyResets} monthly",
                dailyResets, weeklyResets, monthlyResets);
        }
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
}
