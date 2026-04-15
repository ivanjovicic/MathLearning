using MathLearning.Domain.Entities;
using MathLearning.Application.Services;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services.Leaderboard;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
<<<<<<< HEAD

namespace MathLearning.Infrastructure.Services;

public class XpTrackingService : IXpTrackingService
{
    private const int MaxSingleEventXp = 500;
    private const int MaxXpPerHour = 2000;
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromHours(1);

    private readonly ApiDbContext _db;
    private readonly ICosmeticRewardService? _cosmeticRewardService;
=======
using Microsoft.Extensions.Options;
using Npgsql;

namespace MathLearning.Infrastructure.Services;

/// <summary>
/// Helper service for updating user XP across all time periods.
/// </summary>
public class XpTrackingService
{
    private const int MaxConcurrencyRetries = 3;

    private readonly ApiDbContext _db;
    private readonly XpTrackingOptions _options;
>>>>>>> b6bd21f (feat: harden XP audit pipeline and transactional quiz processing)
    private readonly ILogger<XpTrackingService> _logger;

    public XpTrackingService(
        ApiDbContext db,
<<<<<<< HEAD
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
=======
        IOptions<XpTrackingOptions> options,
        ILogger<XpTrackingService> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Adds XP to a user and saves changes immediately.
    /// </summary>
    public async Task<UserProfile> AddXpAsync(string userId, int xpAmount)
    {
        var result = await AddXpWithinTransactionAsync(userId, xpAmount, hintUsed: false, "legacy_add_xp");
        var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null)
            throw new InvalidOperationException($"User profile not found: {userId}");

        _logger.LogInformation(
            "Legacy XP update complete. UserId={UserId} RequestedXp={RequestedXp} AwardedXp={AwardedXp} TotalXp={TotalXp} Reason={Reason}",
            userId,
            xpAmount,
            result.AwardedXp,
            result.TotalXpAfterAward,
            result.Reason);
>>>>>>> b6bd21f (feat: harden XP audit pipeline and transactional quiz processing)

        return profile;
    }

<<<<<<< HEAD
    public async Task ResetTimeBasedXpAsync(string userId, CancellationToken ct = default)
    {
        var startedAt = DateTime.UtcNow;
        var profile = await _db.UserProfiles.FindAsync([userId], ct);
=======
    /// <summary>
    /// Adds XP inside an existing transaction/context and returns awarded amount/reason.
    /// This method intentionally does not begin/commit a transaction.
    /// </summary>
    public async Task<XpAwardResult> AddXpWithinTransactionAsync(
        string userId,
        int requestedXp,
        bool hintUsed,
        string source,
        ApiDbContext? dbContext = null,
        CancellationToken cancellationToken = default)
    {
        var db = dbContext ?? _db;
        if (requestedXp <= 0)
        {
            var current = await db.UserProfiles
                .AsNoTracking()
                .Where(p => p.UserId == userId)
                .Select(p => p.Xp)
                .FirstOrDefaultAsync(cancellationToken);
            return new XpAwardResult(0, current, "xp_not_positive", 0);
        }

        var now = DateTime.UtcNow;
        var retries = 0;

        while (true)
        {
            try
            {
                var profile = await LockProfileForUpdateAsync(db, userId, cancellationToken);

                ResetExpiredPeriods(profile, now);
                var xpAfterPenalty = ApplyHintPenalty(requestedXp, hintUsed);
                var awardDecision = CalculateAwardAfterCaps(profile, xpAfterPenalty);
                var awardedXp = awardDecision.AwardedXp;

                if (awardedXp > 0)
                {
                    profile.Xp += awardedXp;
                    profile.DailyXp += awardedXp;
                    profile.WeeklyXp += awardedXp;
                    profile.MonthlyXp += awardedXp;
                    profile.Level = 1 + (profile.Xp / 100);
                    profile.UpdatedAt = now;
                    profile.LastXpResetDate = now;
                }
                else
                {
                    profile.UpdatedAt = now;
                }

                await db.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "XP award processed. UserId={UserId} Source={Source} RequestedXp={RequestedXp} AwardedXp={AwardedXp} TotalXp={TotalXp} DailyXp={DailyXp} WeeklyXp={WeeklyXp} MonthlyXp={MonthlyXp} Reason={Reason} Retries={Retries}",
                    userId,
                    source,
                    requestedXp,
                    awardedXp,
                    profile.Xp,
                    profile.DailyXp,
                    profile.WeeklyXp,
                    profile.MonthlyXp,
                    awardDecision.Reason,
                    retries);

                return new XpAwardResult(awardedXp, profile.Xp, awardDecision.Reason, retries);
            }
            catch (DbUpdateConcurrencyException ex) when (retries < MaxConcurrencyRetries - 1)
            {
                retries++;
                _logger.LogWarning(
                    ex,
                    "Concurrency conflict while awarding XP. UserId={UserId} Source={Source} Attempt={Attempt}/{MaxAttempts}",
                    userId,
                    source,
                    retries + 1,
                    MaxConcurrencyRetries);

                foreach (var entry in ex.Entries)
                    await entry.ReloadAsync(cancellationToken);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure && retries < MaxConcurrencyRetries - 1)
            {
                retries++;
                _logger.LogWarning(
                    ex,
                    "Serializable transaction retry while awarding XP. UserId={UserId} Source={Source} Attempt={Attempt}/{MaxAttempts}",
                    userId,
                    source,
                    retries + 1,
                    MaxConcurrencyRetries);
            }
        }
    }

    /// <summary>
    /// Manually resets time-based XP for a specific user (admin function).
    /// </summary>
    public async Task ResetTimeBasedXpAsync(string userId)
    {
        var profile = await _db.UserProfiles.FindAsync(userId);
>>>>>>> b6bd21f (feat: harden XP audit pipeline and transactional quiz processing)
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

    private async Task<UserProfile> LockProfileForUpdateAsync(
        ApiDbContext db,
        string userId,
        CancellationToken cancellationToken)
    {
        UserProfile? profile;
        var provider = db.Database.ProviderName ?? string.Empty;
        if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            profile = await db.UserProfiles
                .FromSqlInterpolated($@"SELECT * FROM ""UserProfiles"" WHERE ""UserId"" = {userId} FOR UPDATE")
                .FirstOrDefaultAsync(cancellationToken);
        }
        else
        {
            profile = await db.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        }

        if (profile == null)
            throw new InvalidOperationException($"User profile not found: {userId}");

        return profile;
    }

    private static void ResetExpiredPeriods(UserProfile profile, DateTime now)
    {
        var lastReset = profile.LastXpResetDate ?? DateTime.MinValue;
        var today = now.Date;

        if (lastReset.Date < today)
            profile.DailyXp = 0;

        var weekStart = GetWeekStart(today);
        var lastWeekStart = GetWeekStart(lastReset.Date);
        if (weekStart > lastWeekStart)
            profile.WeeklyXp = 0;

        if (lastReset.Year < today.Year || lastReset.Month < today.Month)
            profile.MonthlyXp = 0;
    }

    private int ApplyHintPenalty(int requestedXp, bool hintUsed)
    {
        if (!hintUsed)
            return requestedXp;

        var percent = Math.Clamp(_options.HintPenaltyPercent, 0, 100);
        var percentPenalty = (requestedXp * percent) / 100;
        var raw = requestedXp - percentPenalty - Math.Max(0, _options.HintPenaltyFlat);
        return Math.Max(0, raw);
    }

    private (int AwardedXp, string Reason) CalculateAwardAfterCaps(UserProfile profile, int requestedXp)
    {
        if (requestedXp <= 0)
            return (0, "penalty_zero");

        if (!_options.EnableXpCaps)
            return (requestedXp, "awarded");

        var awarded = requestedXp;
        var reasons = new List<string>();

        if (_options.DailyXpCap > 0)
        {
            var remaining = Math.Max(0, _options.DailyXpCap - profile.DailyXp);
            if (remaining == 0)
                reasons.Add("daily");
            awarded = Math.Min(awarded, remaining);
        }

        if (_options.WeeklyXpCap > 0)
        {
            var remaining = Math.Max(0, _options.WeeklyXpCap - profile.WeeklyXp);
            if (remaining == 0)
                reasons.Add("weekly");
            awarded = Math.Min(awarded, remaining);
        }

        if (_options.MonthlyXpCap > 0)
        {
            var remaining = Math.Max(0, _options.MonthlyXpCap - profile.MonthlyXp);
            if (remaining == 0)
                reasons.Add("monthly");
            awarded = Math.Min(awarded, remaining);
        }

        if (awarded <= 0)
            return (0, "cap_reached");

        return reasons.Count == 0 ? (awarded, "awarded") : (awarded, $"cap_limited:{string.Join(",", reasons)}");
    }

    /// <summary>
    /// Gets the start of the week (Monday) for a given date.
    /// </summary>
    private static DateTime GetWeekStart(DateTime date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        var daysToSubtract = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
        return date.AddDays(-daysToSubtract).Date;
    }
}
