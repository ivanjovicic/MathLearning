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

        // Detect which XP columns actually exist in the DB to avoid SQL errors
        var hasDaily = await ColumnExistsAsync(db, "DailyXp");
        var hasWeekly = await ColumnExistsAsync(db, "WeeklyXp");
        var hasMonthly = await ColumnExistsAsync(db, "MonthlyXp");
        var hasLastResetDate = await ColumnExistsAsync(db, "LastXpResetDate");

        if (!hasLastResetDate)
        {
            _logger.LogWarning(
                "Skipping XP reset because column {Column} is missing on UserProfiles. Apply pending migrations to repair the schema.",
                "LastXpResetDate");
            return;
        }

        // If all XP columns exist, we can use the EF tracked entities path (simpler)
        if (hasDaily && hasWeekly && hasMonthly)
        {
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

            return;
        }

        // Otherwise, avoid selecting non-existent columns. Fetch minimal profile info and issue safe raw updates
        var minimalProfiles = await db.UserProfiles
            .Where(p => p.LastXpResetDate == null || p.LastXpResetDate < today)
            .Select(p => new { p.UserId, p.LastXpResetDate })
            .ToListAsync();

        if (!minimalProfiles.Any())
            return;

        var dailyCount = 0;
        var weeklyCount = 0;
        var monthlyCount = 0;

        foreach (var p in minimalProfiles)
        {
            var lastReset = p.LastXpResetDate ?? DateTime.MinValue;
            var needsDaily = lastReset.Date < today;
            var needsWeekly = GetWeekStart(today) > GetWeekStart(lastReset.Date);
            var needsMonthly = lastReset.Year < today.Year || lastReset.Month < today.Month;

            var sets = new List<string>();
            if (hasDaily && needsDaily)
            {
                sets.Add("\"DailyXp\" = 0");
                dailyCount++;
            }
            if (hasWeekly && needsWeekly)
            {
                sets.Add("\"WeeklyXp\" = 0");
                weeklyCount++;
            }
            if (hasMonthly && needsMonthly)
            {
                sets.Add("\"MonthlyXp\" = 0");
                monthlyCount++;
            }

            // Always update LastXpResetDate to now for records we touch
            if (sets.Count > 0)
            {
                sets.Add("\"LastXpResetDate\" = @now");
                var sql = $"UPDATE \"UserProfiles\" SET {string.Join(", ", sets)} WHERE \"UserId\" = @id";
                await db.Database.ExecuteSqlRawAsync(sql, new object[] { new Npgsql.NpgsqlParameter("@now", now), new Npgsql.NpgsqlParameter("@id", p.UserId) });
            }
        }

        if (dailyCount > 0 || weeklyCount > 0 || monthlyCount > 0)
        {
            _logger.LogInformation("✅ XP reset completed: {DailyResets} daily, {WeeklyResets} weekly, {MonthlyResets} monthly", dailyCount, weeklyCount, monthlyCount);
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

    private async Task<bool> ColumnExistsAsync(ApiDbContext db, string columnName)
    {
        var conn = db.Database.GetDbConnection();
        try
        {
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            // Use case-insensitive comparison to be resilient to quoted vs non-quoted identifiers
            cmd.CommandText = @"SELECT EXISTS(
                SELECT 1 FROM information_schema.columns
                WHERE lower(table_name) = lower('UserProfiles')
                  AND lower(column_name) = lower(@col)
            );";
            var param = cmd.CreateParameter();
            param.ParameterName = "@col";
            param.Value = columnName;
            cmd.Parameters.Add(param);

            var res = await cmd.ExecuteScalarAsync();
            if (res is bool b)
                return b;
            if (res is int i)
                return i == 1;
            return false;
        }
        catch (Exception ex)
        {
            // If our check fails for any reason, conservatively return false to avoid updates.
            try { _logger.LogWarning(ex, "Could not determine column existence for {Column}", columnName); } catch { }
            return false;
        }
        finally
        {
            try { await conn.CloseAsync(); } catch { }
        }
    }
}
