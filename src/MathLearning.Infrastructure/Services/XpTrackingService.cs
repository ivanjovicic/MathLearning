using MathLearning.Domain.Entities;
using MathLearning.Application.Services;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services.Leaderboard;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MathLearning.Infrastructure.Services;

public class XpTrackingService : IXpTrackingService
{
    private const int MaxSingleEventXp = 500;
    private const int MaxXpPerHour = 2000;
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromHours(1);

    private readonly ApiDbContext _db;
    private readonly ICosmeticRewardService? _cosmeticRewardService;
    private readonly ILogger<XpTrackingService> _logger;

    public XpTrackingService(
        ApiDbContext db,
        ILogger<XpTrackingService> logger,
        ICosmeticRewardService? cosmeticRewardService = null)
    {
        _db = db;
        _logger = logger;
        _cosmeticRewardService = cosmeticRewardService;
    }

    public async Task<UserProfile> AddXpAsync(
        string userId,
        int xpAmount,
        string sourceType = "manual_adjustment",
        string? sourceId = null,
        string? metadataJson = null,
        CancellationToken ct = default)
    {
        var startedAt = DateTime.UtcNow;
        var profile = await _db.UserProfiles.FindAsync([userId], ct);
        if (profile == null)
        {
            throw new InvalidOperationException($"User profile not found: {userId}");
        }

        var effectiveSourceType = string.IsNullOrWhiteSpace(sourceType) ? "manual_adjustment" : sourceType;
        if (!string.IsNullOrWhiteSpace(sourceId))
        {
            var existingTrackedEvent = _db.UserXpEvents.Local.FirstOrDefault(x =>
                x.UserId == userId &&
                x.SourceType == effectiveSourceType &&
                x.SourceId == sourceId);
            if (existingTrackedEvent is not null)
            {
                return profile;
            }

            var existingPersistedEvent = await _db.UserXpEvents
                .AsNoTracking()
                .AnyAsync(
                    x => x.UserId == userId &&
                         x.SourceType == effectiveSourceType &&
                         x.SourceId == sourceId,
                    ct);
            if (existingPersistedEvent)
            {
                return profile;
            }
        }

        var now = DateTime.UtcNow;

        // Anti-cheat: spike + hourly rate checks (flag-and-log; XP still awarded)
        bool isSuspicious = false;
        string? cheatReason = null;
        if (xpAmount > MaxSingleEventXp)
        {
            isSuspicious = true;
            cheatReason = $"Spike: {xpAmount} XP exceeds MaxSingleEventXp ({MaxSingleEventXp})";
        }
        else
        {
            var windowStart = now.Subtract(RateLimitWindow);
            var recentTotal = await _db.UserXpEvents
                .Where(e => e.UserId == userId && e.AwardedAtUtc >= windowStart)
                .SumAsync(e => (int?)e.XpDelta, ct) ?? 0;
            if (recentTotal + xpAmount > MaxXpPerHour)
            {
                isSuspicious = true;
                cheatReason = $"Rate: recent {recentTotal} + {xpAmount} XP exceeds MaxXpPerHour ({MaxXpPerHour})";
            }
        }

        if (isSuspicious)
        {
            _logger.LogWarning(
                "XP anti-cheat triggered. UserId={UserId} SourceType={SourceType} SourceId={SourceId} XpDelta={XpDelta} Reason={Reason}",
                userId, effectiveSourceType, sourceId, xpAmount, cheatReason);
            _db.XpCheatLogs.Add(new XpCheatLog
            {
                UserId = userId,
                XpDelta = xpAmount,
                Reason = cheatReason!,
                SourceType = effectiveSourceType,
                SourceId = sourceId,
                MetadataJson = metadataJson,
                DetectedAtUtc = now
            });
        }

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
            SourceType = effectiveSourceType,
            SourceId = sourceId,
            ValidationStatus = isSuspicious ? "flagged" : "approved",
            IsSuspicious = isSuspicious,
            MetadataJson = metadataJson,
            AwardedAtUtc = now
        };

        _db.UserXpEvents.Add(xpEvent);

        await _db.SaveChangesAsync(ct);
        if (_cosmeticRewardService is not null)
        {
            await _cosmeticRewardService.ProcessProgressRewardsAsync(userId, ct);
        }

        _logger.LogInformation(
            "XP processed. UserId={UserId} SourceType={SourceType} SourceId={SourceId} XpDelta={XpDelta} EffectiveDelta={EffectiveDelta} ElapsedMs={ElapsedMs}",
            userId,
            effectiveSourceType,
            sourceId,
            xpAmount,
            effectiveTotalDelta,
            Math.Round((DateTime.UtcNow - startedAt).TotalMilliseconds, 2));

        return profile;
    }

    public async Task ResetTimeBasedXpAsync(string userId, CancellationToken ct = default)
    {
        var startedAt = DateTime.UtcNow;
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

        await _db.SaveChangesAsync(ct);
        if (_cosmeticRewardService is not null)
        {
            await _cosmeticRewardService.ProcessProgressRewardsAsync(userId, ct);
        }

        _logger.LogInformation(
            "Time-based XP reset processed. UserId={UserId} ElapsedMs={ElapsedMs}",
            userId,
            Math.Round((DateTime.UtcNow - startedAt).TotalMilliseconds, 2));
    }
}
