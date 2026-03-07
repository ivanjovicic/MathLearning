using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services.Leaderboard;

namespace MathLearning.Infrastructure.Services;

public class XpTrackingService
{
    private readonly ApiDbContext _db;
    private readonly SchoolLeaderboardAggregationService _schoolLeaderboardAggregationService;

    public XpTrackingService(
        ApiDbContext db,
        SchoolLeaderboardAggregationService schoolLeaderboardAggregationService)
    {
        _db = db;
        _schoolLeaderboardAggregationService = schoolLeaderboardAggregationService;
    }

    public async Task<UserProfile> AddXpAsync(
        string userId,
        int xpAmount,
        string sourceType = "manual_adjustment",
        string? sourceId = null,
        string? metadataJson = null,
        CancellationToken ct = default)
    {
        var profile = await _db.UserProfiles.FindAsync([userId], ct);
        if (profile == null)
        {
            throw new InvalidOperationException($"User profile not found: {userId}");
        }

        var now = DateTime.UtcNow;
        var previousTotalXp = profile.Xp;
        var previousDailyXp = profile.DailyXp;
        var previousWeeklyXp = profile.WeeklyXp;
        var previousMonthlyXp = profile.MonthlyXp;

        if (profile.LastXpResetDate == null)
        {
            profile.LastXpResetDate = now;
        }

        var lastReset = profile.LastXpResetDate.Value;
        var today = now.Date;
        var lastResetDate = lastReset.Date;

        var dailyReset = lastResetDate < today;
        var weeklyReset = SchoolLeaderboardPeriods.StartOfWeekUtc(today) > SchoolLeaderboardPeriods.StartOfWeekUtc(lastResetDate);
        var monthlyReset = lastReset.Year < today.Year || lastReset.Month < today.Month;

        if (dailyReset)
        {
            profile.DailyXp = 0;
        }

        if (weeklyReset)
        {
            profile.WeeklyXp = 0;
        }

        if (monthlyReset)
        {
            profile.MonthlyXp = 0;
        }

        profile.Xp = Math.Max(0, profile.Xp + xpAmount);
        profile.DailyXp = Math.Max(0, profile.DailyXp + xpAmount);
        profile.WeeklyXp = Math.Max(0, profile.WeeklyXp + xpAmount);
        profile.MonthlyXp = Math.Max(0, profile.MonthlyXp + xpAmount);
        profile.LastXpResetDate = now;
        profile.Level = 1 + (profile.Xp / 100);
        profile.UpdatedAt = now;

        var effectiveTotalDelta = profile.Xp - previousTotalXp;
        var xpEvent = new UserXpEvent
        {
            UserId = userId,
            SchoolId = profile.SchoolId,
            XpDelta = xpAmount,
            ValidatedXpDelta = effectiveTotalDelta,
            SourceType = string.IsNullOrWhiteSpace(sourceType) ? "manual_adjustment" : sourceType,
            SourceId = sourceId,
            ValidationStatus = "approved",
            IsSuspicious = false,
            MetadataJson = metadataJson,
            AwardedAtUtc = now
        };

        _db.UserXpEvents.Add(xpEvent);

        var change = new UserXpChangeContext(
            TotalBefore: previousTotalXp,
            TotalAfter: profile.Xp,
            DailyBefore: dailyReset ? 0 : previousDailyXp,
            DailyAfter: profile.DailyXp,
            WeeklyBefore: weeklyReset ? 0 : previousWeeklyXp,
            WeeklyAfter: profile.WeeklyXp,
            MonthlyBefore: monthlyReset ? 0 : previousMonthlyXp,
            MonthlyAfter: profile.MonthlyXp,
            DailyReset: dailyReset,
            WeeklyReset: weeklyReset,
            MonthlyReset: monthlyReset,
            OccurredAtUtc: now);

        await _schoolLeaderboardAggregationService.ApplyXpChangeAsync(profile, change, ct);
        await _db.SaveChangesAsync(ct);

        return profile;
    }

    public async Task ResetTimeBasedXpAsync(string userId, CancellationToken ct = default)
    {
        var profile = await _db.UserProfiles.FindAsync([userId], ct);
        if (profile == null)
        {
            throw new InvalidOperationException($"User profile not found: {userId}");
        }

        var now = DateTime.UtcNow;
        var previousDailyXp = profile.DailyXp;
        var previousWeeklyXp = profile.WeeklyXp;
        var previousMonthlyXp = profile.MonthlyXp;

        profile.DailyXp = 0;
        profile.WeeklyXp = 0;
        profile.MonthlyXp = 0;
        profile.LastXpResetDate = now;
        profile.UpdatedAt = now;

        var change = new UserXpChangeContext(
            TotalBefore: profile.Xp,
            TotalAfter: profile.Xp,
            DailyBefore: previousDailyXp,
            DailyAfter: 0,
            WeeklyBefore: previousWeeklyXp,
            WeeklyAfter: 0,
            MonthlyBefore: previousMonthlyXp,
            MonthlyAfter: 0,
            DailyReset: false,
            WeeklyReset: false,
            MonthlyReset: false,
            OccurredAtUtc: now);

        await _schoolLeaderboardAggregationService.ApplyXpChangeAsync(profile, change, ct);
        await _db.SaveChangesAsync(ct);
    }
}
