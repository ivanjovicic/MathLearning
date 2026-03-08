using System.Text.Json;
using MathLearning.Application.DTOs.Cosmetics;
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
    private readonly ICosmeticRewardService? _cosmeticRewardService;

    public LeaderboardService(ApiDbContext db, ILogger<LeaderboardService> logger, ICosmeticRewardService? cosmeticRewardService = null)
    {
        _db = db;
        _logger = logger;
        _cosmeticRewardService = cosmeticRewardService;
    }

    public async Task<LeaderboardResponseDto> GetLeaderboardAsync(
        string userId,
        string scope,
        string period,
        int limit,
        string? cursor = null,
        bool includeMe = true)
    {
        limit = Math.Clamp(limit, 1, 200);

        var me = await _db.UserProfiles.FindAsync(userId);
        if (me == null)
        {
            throw new InvalidOperationException("User not found");
        }

        var baseQuery = _db.UserProfiles.AsNoTracking().Where(x => x.LeaderboardOptIn);

        LeaderboardContextDto? context = null;
        baseQuery = scope.ToLowerInvariant() switch
        {
            "school" => ApplySchool(baseQuery, me, ref context),
            "faculty" => ApplyFaculty(baseQuery, me, ref context),
            "friends" => ApplyFriends(baseQuery, userId),
            _ => baseQuery
        };

        var cur = CursorCodec.Decode(cursor);
        if (cur is not null)
        {
            baseQuery = baseQuery.Where(u =>
                ScoreSelector.ScoreOf(u, period) < cur.Score ||
                (ScoreSelector.ScoreOf(u, period) == cur.Score && int.Parse(u.UserId) > cur.Id));
        }

        var orderedQuery = period.ToLowerInvariant() switch
        {
            "day" => baseQuery.OrderByDescending(x => x.DailyXp).ThenBy(x => x.UserId),
            "week" => baseQuery.OrderByDescending(x => x.WeeklyXp).ThenBy(x => x.UserId),
            "month" => baseQuery.OrderByDescending(x => x.MonthlyXp).ThenBy(x => x.UserId),
            _ => baseQuery.OrderByDescending(x => x.Xp).ThenBy(x => x.UserId)
        };

        var page = await orderedQuery
            .Take(limit + 1)
            .Select(u => new
            {
                u.UserId,
                u.DisplayName,
                u.Username,
                u.AvatarUrl,
                u.Streak,
                u.Level,
                Score = ScoreSelector.ScoreOf(u, period)
            })
            .ToListAsync();

        var hasMore = page.Count > limit;
        if (hasMore)
        {
            page.RemoveAt(page.Count - 1);
        }

        var firstRank = 1;
        if (page.Count > 0)
        {
            var first = page[0];
            firstRank = await ComputeRank(scope, period, me, first.Score, int.Parse(first.UserId), userId);
        }

        var items = page.Select((x, i) => new LeaderboardItemDto
        {
            Rank = firstRank + i,
            UserId = x.UserId,
            DisplayName = x.DisplayName ?? x.Username ?? $"User{x.UserId}",
            AvatarUrl = x.AvatarUrl,
            Score = x.Score,
            StreakDays = x.Streak,
            Level = x.Level
        }).ToList();

        string? nextCursor = null;
        if (hasMore && page.Count > 0)
        {
            var last = page[^1];
            nextCursor = CursorCodec.Encode(new LeaderboardCursor(last.Score, int.Parse(last.UserId)));
        }

        LeaderboardMeDto? meDto = null;
        if (includeMe)
        {
            var myScore = ScoreSelector.ScoreOf(me, period);
            var myRank = await ComputeRank(scope, period, me, myScore, int.Parse(me.UserId), userId);
            var totalInScope = await CountScope(scope, me, userId);

            var percentile = totalInScope == 0
                ? 100
                : (int)Math.Ceiling((double)myRank / totalInScope * 100);

            meDto = new LeaderboardMeDto
            {
                Rank = myRank,
                Score = myScore,
                Percentile = percentile,
                Badges = BadgeRules.BuildBadges(scope, percentile, myRank)
            };

            if (_cosmeticRewardService is not null)
            {
                var normalizedScope = string.IsNullOrWhiteSpace(scope) ? "global" : scope.Trim().ToLowerInvariant();
                var normalizedPeriod = string.IsNullOrWhiteSpace(period) ? "all_time" : period.Trim().ToLowerInvariant();
                var leaderboardSourceRef = BuildLeaderboardSourceRef(normalizedScope, normalizedPeriod);

                await _cosmeticRewardService.ProcessRewardSourceAsync(
                    new CosmeticRewardSourceRequest(
                        userId,
                        CosmeticUnlockTypes.Leaderboard,
                        leaderboardSourceRef,
                        JsonSerializer.Serialize(new { scope = normalizedScope, period = normalizedPeriod, rank = myRank, percentile })),
                    CancellationToken.None);

                foreach (var badge in meDto.Badges)
                {
                    await _cosmeticRewardService.ProcessRewardSourceAsync(
                        new CosmeticRewardSourceRequest(
                            userId,
                            CosmeticUnlockTypes.Badge,
                            $"badge:{normalizedScope}:{badge}",
                            JsonSerializer.Serialize(new { scope = normalizedScope, badgeKey = badge, rank = myRank, percentile })),
                        CancellationToken.None);
                }
            }
        }

        return new LeaderboardResponseDto
        {
            Scope = scope,
            Period = period,
            Context = scope == "global" ? null : context,
            Items = items,
            Me = meDto,
            NextCursor = nextCursor
        };
    }

    public async Task<SchoolLeaderboardResponseDto> GetSchoolLeaderboardAsync(
        string userId,
        string period,
        int limit,
        string? cursor = null)
    {
        limit = Math.Clamp(limit, 1, 200);

        var me = await _db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);
        if (me == null)
        {
            throw new InvalidOperationException("User not found");
        }

        var periodInfo = SchoolLeaderboardPeriods.Normalize(period);
        if (!await HasSchoolLeaderboardSchemaAsync(periodInfo.Period))
        {
            return new SchoolLeaderboardResponseDto
            {
                Period = periodInfo.Period,
                PeriodStartUtc = periodInfo.PeriodStartUtc,
                Items = new List<SchoolLeaderboardItemDto>(),
                RankingMetric = "composite_score",
                GeneratedAtUtc = DateTime.UtcNow,
                IsStale = true
            };
        }

        await EnsureCurrentPeriodAsync(period);

        var query = CurrentSchoolScoreQuery(periodInfo);
        var cur = CursorCodec.Decode(cursor);

        if (cur is not null)
        {
            var compositeCursor = FromCursorScore(cur.Score);
            query = query.Where(x =>
                x.CompositeScore < compositeCursor ||
                (x.CompositeScore == compositeCursor && x.SchoolId > cur.Id));
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
            nextCursor = CursorCodec.Encode(new LeaderboardCursor(ToCursorScore(last.CompositeScore), last.SchoolId));
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

                if (_cosmeticRewardService is not null)
                {
                    await _cosmeticRewardService.ProcessRewardSourceAsync(
                        new CosmeticRewardSourceRequest(
                            userId,
                            CosmeticUnlockTypes.SchoolCompetition,
                            BuildSchoolCompetitionSourceRef(periodInfo),
                            JsonSerializer.Serialize(new { period = periodInfo.Period, schoolId = me.SchoolId.Value, placement = mySchoolData.Rank, rank = mySchoolData.Rank })),
                        CancellationToken.None);
                }
            }
        }

        return new SchoolLeaderboardResponseDto
        {
            Period = periodInfo.Period,
            PeriodStartUtc = periodInfo.PeriodStartUtc,
            Items = page.Select(MapSchoolItem).ToList(),
            MySchool = mySchool,
            NextCursor = nextCursor,
            RankingMetric = "composite_score",
            GeneratedAtUtc = DateTime.UtcNow,
            IsStale = page.Any(x => DateTime.UtcNow - x.UpdatedAtUtc > AggregateFreshnessWindow)
        };
    }

    public async Task EnsureCurrentPeriodAsync(string period, CancellationToken ct = default)
    {
        var periodInfo = SchoolLeaderboardPeriods.Normalize(period);
        if (!await HasSchoolLeaderboardSchemaAsync(periodInfo.Period, ct))
        {
            return;
        }

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
        if (!await HasSchoolLeaderboardSchemaAsync(periodInfo.Period, ct))
        {
            _logger.LogWarning(
                "Skipping school leaderboard refresh for period {Period} because required UserProfiles columns are missing. Apply pending migrations.",
                periodInfo.Period);
            return;
        }

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
        if (!await HasSchoolLeaderboardSchemaAsync(periodInfo.Period, ct))
        {
            return;
        }

        await EnsureCurrentPeriodAsync(periodInfo.Period, ct);

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
        if (!await HasSchoolLeaderboardSchemaAsync(periodInfo.Period, ct))
        {
            return null;
        }

        await EnsureCurrentPeriodAsync(period, ct);

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
        if (!await HasSchoolLeaderboardSchemaAsync(periodInfo.Period, ct))
        {
            return new SchoolLeaderboardHistoryResponseDto
            {
                SchoolId = schoolId,
                Period = periodInfo.Period,
                PeriodStartUtc = periodInfo.PeriodStartUtc,
                Points = new List<SchoolLeaderboardHistoryPointDto>()
            };
        }

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

        if (points.Count == 0)
        {
            await CaptureSnapshotAsync(periodInfo.Period, ct);

            points = await _db.SchoolRankHistories.AsNoTracking()
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
        }

        points.Reverse();

        return new SchoolLeaderboardHistoryResponseDto
        {
            SchoolId = schoolId,
            Period = periodInfo.Period,
            PeriodStartUtc = periodInfo.PeriodStartUtc,
            Points = points
        };
    }

    private IQueryable<UserProfile> ApplySchool(IQueryable<UserProfile> query, UserProfile me, ref LeaderboardContextDto? context)
    {
        if (me.SchoolId is null)
        {
            return query.Where(_ => false);
        }

        context = new LeaderboardContextDto
        {
            SchoolId = me.SchoolId,
            SchoolName = me.SchoolName
        };

        return query.Where(x => x.SchoolId == me.SchoolId);
    }

    private IQueryable<UserProfile> ApplyFaculty(IQueryable<UserProfile> query, UserProfile me, ref LeaderboardContextDto? context)
    {
        if (me.FacultyId is null)
        {
            return query.Where(_ => false);
        }

        context = new LeaderboardContextDto
        {
            FacultyId = me.FacultyId,
            FacultyName = me.FacultyName
        };

        return query.Where(x => x.FacultyId == me.FacultyId);
    }

    private IQueryable<UserProfile> ApplyFriends(IQueryable<UserProfile> query, string userId)
    {
        var followees = _db.UserFriends
            .Where(f => f.UserId == userId)
            .Select(f => f.FriendId);

        return query.Where(u => u.UserId == userId || followees.Contains(u.UserId));
    }

    private async Task<int> CountScope(string scope, UserProfile me, string userId)
    {
        var query = _db.UserProfiles.AsNoTracking().Where(u => u.LeaderboardOptIn);

        if (scope == "school" && me.SchoolId is not null)
        {
            query = query.Where(u => u.SchoolId == me.SchoolId);
        }
        else if (scope == "faculty" && me.FacultyId is not null)
        {
            query = query.Where(u => u.FacultyId == me.FacultyId);
        }
        else if (scope == "friends")
        {
            var followees = _db.UserFriends.Where(f => f.UserId == userId).Select(f => f.FriendId);
            query = query.Where(u => u.UserId == userId || followees.Contains(u.UserId));
        }

        return await query.CountAsync();
    }

    private async Task<int> ComputeRank(string scope, string period, UserProfile me, int myScore, int myId, string userId)
    {
        var query = _db.UserProfiles.AsNoTracking().Where(u => u.LeaderboardOptIn);

        if (scope == "school" && me.SchoolId is not null)
        {
            query = query.Where(u => u.SchoolId == me.SchoolId);
        }
        else if (scope == "faculty" && me.FacultyId is not null)
        {
            query = query.Where(u => u.FacultyId == me.FacultyId);
        }
        else if (scope == "friends")
        {
            var followees = _db.UserFriends.Where(f => f.UserId == userId).Select(f => f.FriendId);
            query = query.Where(u => u.UserId == userId || followees.Contains(u.UserId));
        }

        var higher = await query.CountAsync(u =>
            ScoreSelector.ScoreOf(u, period) > myScore ||
            (ScoreSelector.ScoreOf(u, period) == myScore && int.Parse(u.UserId) < myId));

        return higher + 1;
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

    private static string BuildLeaderboardSourceRef(string scope, string period)
    {
        var periodKey = period switch
        {
            "day" => DateTime.UtcNow.ToString("yyyyMMdd"),
            "week" => SchoolLeaderboardPeriods.StartOfWeekUtc(DateTime.UtcNow.Date).ToString("yyyyMMdd"),
            "month" => DateTime.UtcNow.ToString("yyyyMM"),
            _ => "all-time"
        };

        return $"leaderboard:{scope}:{period}:{periodKey}";
    }

    private static string BuildSchoolCompetitionSourceRef(SchoolLeaderboardPeriodInfo periodInfo)
        => $"school-competition:{periodInfo.Period}:{periodInfo.PeriodStartUtc:yyyyMMdd}";

    private async Task<bool> HasSchoolLeaderboardSchemaAsync(string period, CancellationToken ct = default)
    {
        if (!_db.Database.IsRelational())
        {
            return true;
        }

        var requiredColumns = new List<string> { "SchoolId", "LeaderboardOptIn" };
        switch (period)
        {
            case "day":
                requiredColumns.Add("DailyXp");
                break;
            case "month":
                requiredColumns.Add("MonthlyXp");
                break;
            case "all_time":
                requiredColumns.Add("Xp");
                break;
            default:
                requiredColumns.Add("WeeklyXp");
                break;
        }

        foreach (var column in requiredColumns)
        {
            if (!await ColumnExistsAsync(column, ct))
            {
                _logger.LogWarning("Missing required UserProfiles column {Column} for school leaderboard period {Period}.", column, period);
                return false;
            }
        }

        return true;
    }

    private async Task<bool> ColumnExistsAsync(string columnName, CancellationToken ct)
    {
        var conn = _db.Database.GetDbConnection();
        try
        {
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await conn.OpenAsync(ct);
            }

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT EXISTS(
                SELECT 1 FROM information_schema.columns
                WHERE lower(table_name) = lower('UserProfiles')
                  AND lower(column_name) = lower(@col)
            );";

            var param = cmd.CreateParameter();
            param.ParameterName = "@col";
            param.Value = columnName;
            cmd.Parameters.Add(param);

            var result = await cmd.ExecuteScalarAsync(ct);
            return result switch
            {
                bool b => b,
                int i => i == 1,
                long l => l == 1,
                _ => false
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not determine whether UserProfiles.{Column} exists.", columnName);
            return false;
        }
        finally
        {
            try
            {
                await conn.CloseAsync();
            }
            catch
            {
                // Ignore connection close failures in schema probe.
            }
        }
    }

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
