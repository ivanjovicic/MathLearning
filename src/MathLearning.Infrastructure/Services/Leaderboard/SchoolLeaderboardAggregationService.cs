using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Infrastructure.Services.Leaderboard;

public sealed record UserXpChangeContext(
    int TotalBefore,
    int TotalAfter,
    int DailyBefore,
    int DailyAfter,
    int WeeklyBefore,
    int WeeklyAfter,
    int MonthlyBefore,
    int MonthlyAfter,
    bool DailyReset,
    bool WeeklyReset,
    bool MonthlyReset,
    DateTime OccurredAtUtc);

public class SchoolLeaderboardAggregationService
{
    private readonly ApiDbContext _db;

    public SchoolLeaderboardAggregationService(ApiDbContext db)
    {
        _db = db;
    }

    public async Task ApplyXpChangeAsync(UserProfile profile, UserXpChangeContext change, CancellationToken ct = default)
    {
        if (profile.SchoolId is null || !profile.LeaderboardOptIn)
        {
            return;
        }

        var schoolId = profile.SchoolId.Value;
        var touchedPeriods = new List<SchoolLeaderboardPeriodInfo>();

        await ApplyAllTimeAsync(schoolId, change, touchedPeriods, ct);
        await ApplyPeriodAsync(schoolId, "day", change.DailyBefore, change.DailyAfter, change.DailyReset, touchedPeriods, ct);
        await ApplyPeriodAsync(schoolId, "week", change.WeeklyBefore, change.WeeklyAfter, change.WeeklyReset, touchedPeriods, ct);
        await ApplyPeriodAsync(schoolId, "month", change.MonthlyBefore, change.MonthlyAfter, change.MonthlyReset, touchedPeriods, ct);

        if (touchedPeriods.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        foreach (var period in touchedPeriods
                     .DistinctBy(x => (x.Period, x.PeriodStartUtc)))
        {
            await RecomputeRanksForPeriodAsync(period, change.OccurredAtUtc, ct);
        }
    }

    private async Task ApplyAllTimeAsync(
        int schoolId,
        UserXpChangeContext change,
        List<SchoolLeaderboardPeriodInfo> touchedPeriods,
        CancellationToken ct)
    {
        var periodInfo = SchoolLeaderboardPeriods.Normalize("all_time");
        var row = await GetOrRecomputeRowAsync(schoolId, periodInfo, forceRecompute: false, change.OccurredAtUtc, ct);
        if (row is null)
        {
            return;
        }

        var delta = change.TotalAfter - change.TotalBefore;
        if (delta != 0)
        {
            row.XpTotal = Math.Max(0, row.XpTotal + delta);
            row.ActiveStudents = AdjustActiveCount(row.ActiveStudents, change.TotalBefore, change.TotalAfter);
            row.UpdatedAtUtc = change.OccurredAtUtc;
            SchoolLeaderboardScoring.UpdateDerivedMetrics(row);
            touchedPeriods.Add(periodInfo);
        }
    }

    private async Task ApplyPeriodAsync(
        int schoolId,
        string period,
        int before,
        int after,
        bool reset,
        List<SchoolLeaderboardPeriodInfo> touchedPeriods,
        CancellationToken ct)
    {
        var periodInfo = SchoolLeaderboardPeriods.Normalize(period);
        if (reset)
        {
            await GetOrRecomputeRowAsync(schoolId, periodInfo, forceRecompute: true, DateTime.UtcNow, ct);
            touchedPeriods.Add(periodInfo);
            return;
        }

        var delta = after - before;
        if (delta == 0)
        {
            return;
        }

        var row = await GetOrRecomputeRowAsync(schoolId, periodInfo, forceRecompute: false, DateTime.UtcNow, ct);
        if (row is null)
        {
            return;
        }

        row.XpTotal = Math.Max(0, row.XpTotal + delta);
        row.ActiveStudents = AdjustActiveCount(row.ActiveStudents, before, after);
        row.UpdatedAtUtc = DateTime.UtcNow;
        SchoolLeaderboardScoring.UpdateDerivedMetrics(row);
        touchedPeriods.Add(periodInfo);
    }

    private async Task<SchoolScoreAggregate?> GetOrRecomputeRowAsync(
        int schoolId,
        SchoolLeaderboardPeriodInfo periodInfo,
        bool forceRecompute,
        DateTime now,
        CancellationToken ct)
    {
        var row = await _db.SchoolScoreAggregates
            .FirstOrDefaultAsync(x =>
                x.SchoolId == schoolId &&
                x.Period == periodInfo.Period &&
                x.PeriodStartUtc == periodInfo.PeriodStartUtc, ct);

        if (row is null || forceRecompute || row.EligibleStudents <= 0)
        {
            return await RecomputeSchoolRowAsync(schoolId, periodInfo, now, ct);
        }

        return row;
    }

    private async Task<SchoolScoreAggregate?> RecomputeSchoolRowAsync(
        int schoolId,
        SchoolLeaderboardPeriodInfo periodInfo,
        DateTime now,
        CancellationToken ct)
    {
        var snapshot = await BuildSchoolMetricQuery(schoolId, periodInfo.Period).FirstOrDefaultAsync(ct);
        var existing = await _db.SchoolScoreAggregates
            .FirstOrDefaultAsync(x =>
                x.SchoolId == schoolId &&
                x.Period == periodInfo.Period &&
                x.PeriodStartUtc == periodInfo.PeriodStartUtc, ct);

        if (snapshot is null)
        {
            if (existing is not null)
            {
                _db.SchoolScoreAggregates.Remove(existing);
            }

            return null;
        }

        var row = existing ?? new SchoolScoreAggregate
        {
            SchoolId = schoolId,
            Period = periodInfo.Period,
            PeriodStartUtc = periodInfo.PeriodStartUtc
        };

        row.XpTotal = snapshot.XpTotal;
        row.ActiveStudents = snapshot.ActiveStudents;
        row.EligibleStudents = snapshot.EligibleStudents;
        row.UpdatedAtUtc = now;
        SchoolLeaderboardScoring.UpdateDerivedMetrics(row);

        if (existing is null)
        {
            _db.SchoolScoreAggregates.Add(row);
        }

        return row;
    }

    private async Task RecomputeRanksForPeriodAsync(
        SchoolLeaderboardPeriodInfo periodInfo,
        DateTime now,
        CancellationToken ct)
    {
        var rows = await _db.SchoolScoreAggregates
            .Where(x => x.Period == periodInfo.Period && x.PeriodStartUtc == periodInfo.PeriodStartUtc)
            .ToListAsync(ct);

        if (rows.Count == 0)
        {
            return;
        }

        SchoolLeaderboardScoring.RecomputeScoresAndRanks(rows);
        foreach (var row in rows)
        {
            row.UpdatedAtUtc = now;
        }
    }

    private IQueryable<SchoolMetricSnapshot> BuildSchoolMetricQuery(int schoolId, string period)
        => period switch
        {
            "day" => _db.UserProfiles.AsNoTracking()
                .Where(x => x.LeaderboardOptIn && x.SchoolId == schoolId)
                .GroupBy(x => x.SchoolId!.Value)
                .Select(g => new SchoolMetricSnapshot
                {
                    XpTotal = g.Sum(x => x.DailyXp),
                    ActiveStudents = g.Count(x => x.DailyXp > 0),
                    EligibleStudents = g.Count()
                }),
            "week" => _db.UserProfiles.AsNoTracking()
                .Where(x => x.LeaderboardOptIn && x.SchoolId == schoolId)
                .GroupBy(x => x.SchoolId!.Value)
                .Select(g => new SchoolMetricSnapshot
                {
                    XpTotal = g.Sum(x => x.WeeklyXp),
                    ActiveStudents = g.Count(x => x.WeeklyXp > 0),
                    EligibleStudents = g.Count()
                }),
            "month" => _db.UserProfiles.AsNoTracking()
                .Where(x => x.LeaderboardOptIn && x.SchoolId == schoolId)
                .GroupBy(x => x.SchoolId!.Value)
                .Select(g => new SchoolMetricSnapshot
                {
                    XpTotal = g.Sum(x => x.MonthlyXp),
                    ActiveStudents = g.Count(x => x.MonthlyXp > 0),
                    EligibleStudents = g.Count()
                }),
            _ => _db.UserProfiles.AsNoTracking()
                .Where(x => x.LeaderboardOptIn && x.SchoolId == schoolId)
                .GroupBy(x => x.SchoolId!.Value)
                .Select(g => new SchoolMetricSnapshot
                {
                    XpTotal = g.Sum(x => x.Xp),
                    ActiveStudents = g.Count(x => x.Xp > 0),
                    EligibleStudents = g.Count()
                })
        };

    private static int AdjustActiveCount(int currentActiveStudents, int before, int after)
    {
        if (before <= 0 && after > 0)
        {
            return currentActiveStudents + 1;
        }

        if (before > 0 && after <= 0)
        {
            return Math.Max(0, currentActiveStudents - 1);
        }

        return currentActiveStudents;
    }

    private sealed class SchoolMetricSnapshot
    {
        public int XpTotal { get; init; }
        public int ActiveStudents { get; init; }
        public int EligibleStudents { get; init; }
    }
}
