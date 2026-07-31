using MathLearning.Application.DTOs.Leaderboard;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services.Leaderboard;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MathLearning.Infrastructure.Services;

public class LeaderboardService : ILeaderboardService, ISchoolLeaderboardService
{
    private static readonly TimeSpan AggregateFreshnessWindow = TimeSpan.FromMinutes(5);
    private readonly ApiDbContext _db;
    private readonly ILogger<LeaderboardService> _logger;
    public LeaderboardService(
        ApiDbContext db,
        ILogger<LeaderboardService> logger,
        ICosmeticRewardService? cosmeticRewardService = null)
    {
        _db = db;
        _logger = logger;
    }


    public async Task<SchoolLeaderboardResponseDto> GetSchoolLeaderboardAsync(
        string userId,
        string period,
        int limit,
        string? cursor = null)
    {
        var startedAt = DateTime.UtcNow;
        limit = Math.Clamp(limit, 1, 200);

        var me = await _db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);
        if (me == null)
        {
            throw new InvalidOperationException("User not found");
        }

        var periodInfo = SchoolLeaderboardPeriods.Normalize(period);
        var query = CurrentSchoolScoreQuery(periodInfo);
        var schoolCursorId = CursorCodec.DecodeSchoolId(cursor);

        if (schoolCursorId is not null)
        {
            var decoded = CursorCodec.Decode(cursor)!;
            var compositeCursor = FromCursorScore(decoded.Score);
            query = query.Where(x =>
                x.CompositeScore < compositeCursor ||
                (x.CompositeScore == compositeCursor && x.SchoolId > schoolCursorId.Value));
        }

        var page = await query
            .OrderByDescending(x => x.CompositeScore)
            .ThenBy(x => x.SchoolId)
            .Take(limit + 1)
            .Select(x => new SchoolAggregateProjection
            {
                Rank = x.Rank,
                SchoolId = x.SchoolId,
                SchoolName = x.School != null ? x.School.Name : $"School #{x.SchoolId}",
                XpTotal = x.XpTotal,
                ActiveStudents = x.ActiveStudents,
                EligibleStudents = x.EligibleStudents,
                ParticipationRate = x.ParticipationRate,
                AverageXpPerActiveStudent = x.AverageXpPerActiveStudent,
                CompositeScore = x.CompositeScore,
                UpdatedAtUtc = x.UpdatedAtUtc
            })
            .ToListAsync();

        var hasMore = page.Count > limit;
        if (hasMore)
        {
            page.RemoveAt(page.Count - 1);
        }

        string? nextCursor = null;
        if (hasMore && page.Count > 0)
        {
            var last = page[^1];
            nextCursor = CursorCodec.EncodeSchool(ToCursorScore(last.CompositeScore), last.SchoolId);
        }

        SchoolLeaderboardItemDto? mySchool = null;
        if (me.SchoolId is not null)
        {
            var mySchoolData = await CurrentSchoolScoreQuery(periodInfo)
                .Where(x => x.SchoolId == me.SchoolId.Value)
                .Select(x => new SchoolAggregateProjection
                {
                    Rank = x.Rank,
                    SchoolId = x.SchoolId,
                    SchoolName = x.School != null ? x.School.Name : $"School #{x.SchoolId}",
                    XpTotal = x.XpTotal,
                    ActiveStudents = x.ActiveStudents,
                    EligibleStudents = x.EligibleStudents,
                    ParticipationRate = x.ParticipationRate,
                    AverageXpPerActiveStudent = x.AverageXpPerActiveStudent,
                    CompositeScore = x.CompositeScore,
                    UpdatedAtUtc = x.UpdatedAtUtc
                })
                .FirstOrDefaultAsync();

            if (mySchoolData is not null)
            {
                mySchool = MapSchoolItem(mySchoolData);
            }
        }

        var response = new SchoolLeaderboardResponseDto
        {
            Period = periodInfo.Period,
            PeriodStartUtc = periodInfo.PeriodStartUtc,
            Items = page.Select(MapSchoolItem).ToList(),
            MySchool = mySchool,
            NextCursor = nextCursor,
            RankingMetric = "composite_score",
            GeneratedAtUtc = DateTime.UtcNow,
            IsStale = page.Count == 0 || page.Any(x => DateTime.UtcNow - x.UpdatedAtUtc > AggregateFreshnessWindow)
        };

        _logger.LogInformation(
            "School leaderboard query executed. Period={Period} Limit={Limit} ItemCount={ItemCount} ElapsedMs={ElapsedMs}",
            periodInfo.Period,
            limit,
            response.Items.Count,
            Math.Round((DateTime.UtcNow - startedAt).TotalMilliseconds, 2));

        return response;
    }

    public async Task EnsureCurrentPeriodAsync(string period, CancellationToken ct = default)
    {
        var periodInfo = SchoolLeaderboardPeriods.Normalize(period);
        var cutoff = DateTime.UtcNow - AggregateFreshnessWindow;

        var hasFreshAggregate = await _db.SchoolScoreAggregates.AsNoTracking()
            .Where(x => x.Period == periodInfo.Period && x.PeriodStartUtc == periodInfo.PeriodStartUtc)
            .AnyAsync(x => x.UpdatedAtUtc >= cutoff, ct);

        if (!hasFreshAggregate)
        {
            await RefreshCurrentPeriodAsync(periodInfo.Period, ct);
        }
    }

    public async Task RefreshCurrentPeriodAsync(string period, CancellationToken ct = default)
    {
        var periodInfo = SchoolLeaderboardPeriods.Normalize(period);
        var now = DateTime.UtcNow;
        var raw = await BuildRawSchoolMetricsQuery(periodInfo.Period).ToListAsync(ct);
        var existing = await _db.SchoolScoreAggregates
            .Where(x => x.Period == periodInfo.Period && x.PeriodStartUtc == periodInfo.PeriodStartUtc)
            .ToDictionaryAsync(x => x.SchoolId, ct);

        if (raw.Count == 0)
        {
            if (existing.Count > 0)
            {
                _db.SchoolScoreAggregates.RemoveRange(existing.Values);
                await _db.SaveChangesAsync(ct);
            }

            return;
        }

        var computed = BuildComputedScores(raw, now);
        var seenSchoolIds = new HashSet<int>();

        foreach (var item in computed)
        {
            seenSchoolIds.Add(item.SchoolId);

            if (!existing.TryGetValue(item.SchoolId, out var aggregate))
            {
                aggregate = new SchoolScoreAggregate
                {
                    SchoolId = item.SchoolId,
                    Period = periodInfo.Period,
                    PeriodStartUtc = periodInfo.PeriodStartUtc
                };
                _db.SchoolScoreAggregates.Add(aggregate);
            }

            aggregate.XpTotal = item.XpTotal;
            aggregate.ActiveStudents = item.ActiveStudents;
            aggregate.EligibleStudents = item.EligibleStudents;
            aggregate.AverageXpPerActiveStudent = item.AverageXpPerActiveStudent;
            aggregate.ParticipationRate = item.ParticipationRate;
            aggregate.CompositeScore = item.CompositeScore;
            aggregate.Rank = item.Rank;
            aggregate.UpdatedAtUtc = now;
        }

        var staleRows = existing.Values.Where(x => !seenSchoolIds.Contains(x.SchoolId)).ToList();
        if (staleRows.Count > 0)
        {
            _db.SchoolScoreAggregates.RemoveRange(staleRows);
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task RefreshAllCurrentPeriodsAsync(CancellationToken ct = default)
    {
        foreach (var period in SchoolLeaderboardPeriods.All)
        {
            await RefreshCurrentPeriodAsync(period, ct);
        }
    }

    public async Task CaptureSnapshotAsync(string period, CancellationToken ct = default)
    {
        var periodInfo = SchoolLeaderboardPeriods.Normalize(period);
        var now = DateTime.UtcNow;
        var recentlyCaptured = await _db.SchoolRankHistories.AsNoTracking()
            .AnyAsync(x =>
                x.Period == periodInfo.Period &&
                x.PeriodStartUtc == periodInfo.PeriodStartUtc &&
                x.SnapshotTimeUtc >= now.AddMinutes(-20), ct);

        if (recentlyCaptured)
        {
            return;
        }

        var currentRows = await CurrentSchoolScoreQuery(periodInfo)
            .Select(x => new SchoolRankHistory
            {
                SchoolId = x.SchoolId,
                Period = x.Period,
                PeriodStartUtc = x.PeriodStartUtc,
                Rank = x.Rank,
                XpTotal = x.XpTotal,
                ActiveStudents = x.ActiveStudents,
                ParticipationRate = x.ParticipationRate,
                CompositeScore = x.CompositeScore,
                WeightedXp = x.WeightedXp,
                SnapshotTimeUtc = now
            })
            .ToListAsync(ct);

        if (currentRows.Count == 0)
        {
            return;
        }

        _db.SchoolRankHistories.AddRange(currentRows);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<SchoolLeaderboardDetailDto?> GetSchoolLeaderboardDetailsAsync(
        int schoolId,
        string period,
        int neighbors = 2,
        CancellationToken ct = default)
    {
        var periodInfo = SchoolLeaderboardPeriods.Normalize(period);
        var school = await CurrentSchoolScoreQuery(periodInfo)
            .Where(x => x.SchoolId == schoolId)
            .Select(x => new SchoolAggregateProjection
            {
                Rank = x.Rank,
                SchoolId = x.SchoolId,
                SchoolName = x.School != null ? x.School.Name : $"School #{x.SchoolId}",
                XpTotal = x.XpTotal,
                ActiveStudents = x.ActiveStudents,
                EligibleStudents = x.EligibleStudents,
                ParticipationRate = x.ParticipationRate,
                AverageXpPerActiveStudent = x.AverageXpPerActiveStudent,
                CompositeScore = x.CompositeScore,
                UpdatedAtUtc = x.UpdatedAtUtc
            })
            .FirstOrDefaultAsync(ct);

        if (school is null)
        {
            return null;
        }

        var minRank = Math.Max(1, school.Rank - Math.Max(1, neighbors));
        var maxRank = school.Rank + Math.Max(1, neighbors);

        var nearby = await CurrentSchoolScoreQuery(periodInfo)
            .Where(x => x.Rank >= minRank && x.Rank <= maxRank && x.SchoolId != schoolId)
            .OrderBy(x => x.Rank)
            .Select(x => new SchoolAggregateProjection
            {
                Rank = x.Rank,
                SchoolId = x.SchoolId,
                SchoolName = x.School != null ? x.School.Name : $"School #{x.SchoolId}",
                XpTotal = x.XpTotal,
                ActiveStudents = x.ActiveStudents,
                EligibleStudents = x.EligibleStudents,
                ParticipationRate = x.ParticipationRate,
                AverageXpPerActiveStudent = x.AverageXpPerActiveStudent,
                CompositeScore = x.CompositeScore,
                UpdatedAtUtc = x.UpdatedAtUtc
            })
            .ToListAsync(ct);

        return new SchoolLeaderboardDetailDto
        {
            Period = periodInfo.Period,
            PeriodStartUtc = periodInfo.PeriodStartUtc,
            School = MapSchoolItem(school),
            NearbySchools = nearby.Select(MapSchoolItem).ToList(),
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    public async Task<SchoolLeaderboardHistoryResponseDto> GetSchoolLeaderboardHistoryAsync(
        int schoolId,
        string period,
        int take = 30,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 120);
        var periodInfo = SchoolLeaderboardPeriods.Normalize(period);
        var points = await _db.SchoolRankHistories.AsNoTracking()
            .Where(x => x.SchoolId == schoolId && x.Period == periodInfo.Period && x.PeriodStartUtc == periodInfo.PeriodStartUtc)
            .OrderByDescending(x => x.SnapshotTimeUtc)
            .Take(take)
            .Select(x => new SchoolLeaderboardHistoryPointDto
            {
                SnapshotTimeUtc = x.SnapshotTimeUtc,
                Rank = x.Rank,
                Score = x.XpTotal,
                ActiveStudents = x.ActiveStudents,
                ParticipationRate = x.ParticipationRate,
                CompositeScore = x.CompositeScore
            })
            .ToListAsync(ct);

        points.Reverse();

        return new SchoolLeaderboardHistoryResponseDto
        {
            SchoolId = schoolId,
            Period = periodInfo.Period,
            PeriodStartUtc = periodInfo.PeriodStartUtc,
            Points = points
        };
    }

    private IQueryable<SchoolScoreAggregate> CurrentSchoolScoreQuery(SchoolLeaderboardPeriodInfo periodInfo)
        => _db.SchoolScoreAggregates.AsNoTracking()
            .Where(x => x.Period == periodInfo.Period && x.PeriodStartUtc == periodInfo.PeriodStartUtc);

    private IQueryable<RawSchoolMetric> BuildRawSchoolMetricsQuery(string period)
        => period switch
        {
            "day" => _db.UserProfiles.AsNoTracking()
                .Where(x => x.LeaderboardOptIn && x.SchoolId != null)
                .GroupBy(x => x.SchoolId!.Value)
                .Select(g => new RawSchoolMetric
                {
                    SchoolId = g.Key,
                    EligibleStudents = g.Count(),
                    ActiveStudents = g.Count(x => x.DailyXp > 0),
                    XpTotal = g.Sum(x => x.DailyXp)
                }),
            "week" => _db.UserProfiles.AsNoTracking()
                .Where(x => x.LeaderboardOptIn && x.SchoolId != null)
                .GroupBy(x => x.SchoolId!.Value)
                .Select(g => new RawSchoolMetric
                {
                    SchoolId = g.Key,
                    EligibleStudents = g.Count(),
                    ActiveStudents = g.Count(x => x.WeeklyXp > 0),
                    XpTotal = g.Sum(x => x.WeeklyXp)
                }),
            "month" => _db.UserProfiles.AsNoTracking()
                .Where(x => x.LeaderboardOptIn && x.SchoolId != null)
                .GroupBy(x => x.SchoolId!.Value)
                .Select(g => new RawSchoolMetric
                {
                    SchoolId = g.Key,
                    EligibleStudents = g.Count(),
                    ActiveStudents = g.Count(x => x.MonthlyXp > 0),
                    XpTotal = g.Sum(x => x.MonthlyXp)
                }),
            _ => _db.UserProfiles.AsNoTracking()
                .Where(x => x.LeaderboardOptIn && x.SchoolId != null)
                .GroupBy(x => x.SchoolId!.Value)
                .Select(g => new RawSchoolMetric
                {
                    SchoolId = g.Key,
                    EligibleStudents = g.Count(),
                    ActiveStudents = g.Count(x => x.Xp > 0),
                    XpTotal = g.Sum(x => x.Xp)
                })
        };

    private static List<ComputedSchoolMetric> BuildComputedScores(List<RawSchoolMetric> raw, DateTime now)
    {
        var rows = raw.Select(x => new SchoolScoreAggregate
        {
            SchoolId = x.SchoolId,
            XpTotal = x.XpTotal,
            ActiveStudents = x.ActiveStudents,
            EligibleStudents = x.EligibleStudents,
            UpdatedAtUtc = now
        }).ToList();

        SchoolLeaderboardScoring.RecomputeScoresAndRanks(rows);

        return rows.Select(x => new ComputedSchoolMetric
        {
            SchoolId = x.SchoolId,
            XpTotal = x.XpTotal,
            ActiveStudents = x.ActiveStudents,
            EligibleStudents = x.EligibleStudents,
            AverageXpPerActiveStudent = x.AverageXpPerActiveStudent,
            ParticipationRate = x.ParticipationRate,
            CompositeScore = x.CompositeScore,
            Rank = x.Rank,
            UpdatedAtUtc = x.UpdatedAtUtc
        }).ToList();
    }

    private static SchoolLeaderboardItemDto MapSchoolItem(SchoolAggregateProjection projection)
        => new()
        {
            Rank = projection.Rank,
            SchoolId = projection.SchoolId,
            SchoolName = projection.SchoolName,
            Score = projection.XpTotal,
            Members = projection.EligibleStudents,
            RankingScore = projection.CompositeScore,
            ActiveStudents = projection.ActiveStudents,
            EligibleStudents = projection.EligibleStudents,
            ParticipationRate = projection.ParticipationRate,
            AverageXpPerActiveStudent = projection.AverageXpPerActiveStudent,
            UpdatedAtUtc = projection.UpdatedAtUtc
        };

    private static int ToCursorScore(decimal compositeScore)
        => (int)Math.Round(compositeScore * 10000m, MidpointRounding.AwayFromZero);

    private static decimal FromCursorScore(int score)
        => score / 10000m;

    private sealed class RawSchoolMetric
    {
        public int SchoolId { get; init; }
        public int XpTotal { get; init; }
        public int ActiveStudents { get; init; }
        public int EligibleStudents { get; init; }
    }

    private sealed class ComputedSchoolMetric
    {
        public int SchoolId { get; init; }
        public int XpTotal { get; init; }
        public int ActiveStudents { get; init; }
        public int EligibleStudents { get; init; }
        public decimal AverageXpPerActiveStudent { get; init; }
        public decimal ParticipationRate { get; init; }
        public decimal CompositeScore { get; set; }
        public int Rank { get; set; }
        public DateTime UpdatedAtUtc { get; init; }
    }

    private sealed class SchoolAggregateProjection
    {
        public int Rank { get; init; }
        public int SchoolId { get; init; }
        public string SchoolName { get; init; } = string.Empty;
        public int XpTotal { get; init; }
        public int ActiveStudents { get; init; }
        public int EligibleStudents { get; init; }
        public decimal ParticipationRate { get; init; }
        public decimal AverageXpPerActiveStudent { get; init; }
        public decimal CompositeScore { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
    }

}
