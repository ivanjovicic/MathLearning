using MathLearning.Application.DTOs.Leaderboard;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services.Leaderboard;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Infrastructure.Services;

/// <summary>
/// Service for managing and retrieving leaderboard data with cursor-based pagination
/// </summary>
using MathLearning.Application.Services;

public class LeaderboardService : ILeaderboardService
{
    private readonly ApiDbContext _db;

    public LeaderboardService(ApiDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Gets the leaderboard with specified scope, period, and cursor-based pagination
    /// </summary>
    /// <param name="userId">Current user's ID</param>
    /// <param name="scope">Scope: global, school, faculty, friends</param>
    /// <param name="period">Period: all_time, week, month, day</param>
    /// <param name="limit">Maximum number of top users to return</param>
    /// <param name="cursor">Cursor for pagination (base64 encoded)</param>
    /// <param name="includeMe">Whether to include current user's position and badges</param>
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
            throw new InvalidOperationException("User not found");

        // Base query: users who opted in
        var baseQuery = _db.UserProfiles.AsNoTracking().Where(x => x.LeaderboardOptIn);

        // Apply scope filtering
        LeaderboardContextDto? context = null;
        baseQuery = scope.ToLower() switch
        {
            "school" => ApplySchool(baseQuery, me, ref context),
            "faculty" => ApplyFaculty(baseQuery, me, ref context),
            "friends" => ApplyFriends(baseQuery, userId),
            _ => baseQuery // global
        };

        // Decode cursor for keyset pagination
        var cur = CursorCodec.Decode(cursor);
        if (cur is not null)
        {
            // Keyset filter: score < cursor.Score OR (score == cursor.Score AND id > cursor.Id)
            baseQuery = baseQuery.Where(u =>
                ScoreSelector.ScoreOf(u, period) < cur.Score ||
                (ScoreSelector.ScoreOf(u, period) == cur.Score && int.Parse(u.UserId) > cur.Id));
        }

        // Order by score desc, then by userId asc (stable ordering)
        var orderedQuery = period.ToLower() switch
        {
            "day" => baseQuery.OrderByDescending(x => x.DailyXp).ThenBy(x => x.UserId),
            "week" => baseQuery.OrderByDescending(x => x.WeeklyXp).ThenBy(x => x.UserId),
            "month" => baseQuery.OrderByDescending(x => x.MonthlyXp).ThenBy(x => x.UserId),
            _ => baseQuery.OrderByDescending(x => x.Xp).ThenBy(x => x.UserId)
        };

        // Fetch one extra for next cursor detection
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
        if (hasMore) page.RemoveAt(page.Count - 1);

        // Calculate rank offset for the first item
        int firstRank = 1;
        if (page.Count > 0)
        {
            var first = page[0];
            firstRank = await ComputeRank(scope, period, me, first.Score, int.Parse(first.UserId), userId);
        }

        // Build items with ranks
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

        // Generate next cursor
        string? nextCursor = null;
        if (hasMore && page.Count > 0)
        {
            var last = page[^1];
            nextCursor = CursorCodec.Encode(new LeaderboardCursor(last.Score, int.Parse(last.UserId)));
        }

        // Build "Me" DTO with percentile and badges
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
        }

        return new LeaderboardResponseDto
        {
            Scope = scope,
            Period = period,
            Context = (scope == "global") ? null : context,
            Items = items,
            Me = meDto,
            NextCursor = nextCursor
        };
    }

    /// <summary>
    /// Gets school vs school aggregate leaderboard
    /// </summary>
    public virtual async Task<SchoolLeaderboardResponseDto> GetSchoolLeaderboardAsync(
        string userId,
        string period,
        int limit,
        string? cursor = null)
    {
        limit = Math.Clamp(limit, 1, 200);
        var cur = CursorCodec.Decode(cursor);

        var me = await _db.UserProfiles.FindAsync(userId);
        if (me == null)
            throw new InvalidOperationException("User not found");

        // Group users by school and sum their XP
        var baseQuery = _db.UserProfiles.AsNoTracking()
            .Where(u => u.LeaderboardOptIn && u.SchoolId != null)
            .GroupBy(u => u.SchoolId!.Value)
            .Select(g => new
            {
                SchoolId = g.Key,
                Score = period.ToLower() == "week"
                    ? g.Sum(u => u.WeeklyXp)
                    : period.ToLower() == "month"
                        ? g.Sum(u => u.MonthlyXp)
                        : period.ToLower() == "day"
                            ? g.Sum(u => u.DailyXp)
                            : g.Sum(u => u.Xp),
                Members = g.Count()
            });

        // Apply cursor filter before ordering
        if (cur is not null)
        {
            baseQuery = baseQuery.Where(x => x.Score < cur.Score || (x.Score == cur.Score && x.SchoolId > cur.Id));
        }

        var query = baseQuery
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.SchoolId);

        var page = await query.Take(limit + 1).ToListAsync();
        var hasMore = page.Count > limit;
        if (hasMore) page.RemoveAt(page.Count - 1);

        // Fetch school names
        var schoolIds = page.Select(x => x.SchoolId).ToList();
        var schoolNames = await _db.Schools.AsNoTracking()
            .Where(s => schoolIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name);

        // Calculate rank offset
        int firstRank = 1;
        if (page.Count > 0)
        {
            var first = page[0];
            var higher = await _db.UserProfiles.AsNoTracking()
                .Where(u => u.LeaderboardOptIn && u.SchoolId != null)
                .GroupBy(u => u.SchoolId!.Value)
                .Select(g => new
                {
                    SchoolId = g.Key,
                    Score = period.ToLower() == "week"
                        ? g.Sum(u => u.WeeklyXp)
                        : period.ToLower() == "month"
                            ? g.Sum(u => u.MonthlyXp)
                            : period.ToLower() == "day"
                                ? g.Sum(u => u.DailyXp)
                                : g.Sum(u => u.Xp)
                })
                .CountAsync(x => x.Score > first.Score || (x.Score == first.Score && x.SchoolId < first.SchoolId));

            firstRank = higher + 1;
        }

        var items = page.Select((x, i) => new SchoolLeaderboardItemDto
        {
            Rank = firstRank + i,
            SchoolId = x.SchoolId,
            SchoolName = schoolNames.GetValueOrDefault(x.SchoolId, $"School #{x.SchoolId}"),
            Score = x.Score,
            Members = x.Members
        }).ToList();

        string? nextCursor = null;
        if (hasMore && page.Count > 0)
        {
            var last = page[^1];
            nextCursor = CursorCodec.Encode(new LeaderboardCursor(last.Score, last.SchoolId));
        }

        // Calculate "My School" position
        SchoolLeaderboardItemDto? mySchool = null;
        if (me.SchoolId != null)
        {
            var mySchoolData = await _db.UserProfiles.AsNoTracking()
                .Where(u => u.SchoolId == me.SchoolId && u.LeaderboardOptIn)
                .GroupBy(u => u.SchoolId!.Value)
                .Select(g => new
                {
                    SchoolId = g.Key,
                    Score = period.ToLower() == "week"
                        ? g.Sum(u => u.WeeklyXp)
                        : period.ToLower() == "month"
                            ? g.Sum(u => u.MonthlyXp)
                            : period.ToLower() == "day"
                                ? g.Sum(u => u.DailyXp)
                                : g.Sum(u => u.Xp),
                    Members = g.Count()
                })
                .FirstOrDefaultAsync();

            if (mySchoolData != null)
            {
                var myRank = await _db.UserProfiles.AsNoTracking()
                    .Where(u => u.LeaderboardOptIn && u.SchoolId != null)
                    .GroupBy(u => u.SchoolId!.Value)
                    .Select(g => new
                    {
                        SchoolId = g.Key,
                        Score = period.ToLower() == "week"
                            ? g.Sum(u => u.WeeklyXp)
                            : period.ToLower() == "month"
                                ? g.Sum(u => u.MonthlyXp)
                                : period.ToLower() == "day"
                                    ? g.Sum(u => u.DailyXp)
                                    : g.Sum(u => u.Xp)
                    })
                    .CountAsync(x => x.Score > mySchoolData.Score ||
                        (x.Score == mySchoolData.Score && x.SchoolId < mySchoolData.SchoolId)) + 1;

                var mySchoolName = await _db.Schools.AsNoTracking()
                    .Where(s => s.Id == mySchoolData.SchoolId)
                    .Select(s => s.Name)
                    .FirstOrDefaultAsync();

                mySchool = new SchoolLeaderboardItemDto
                {
                    Rank = myRank,
                    SchoolId = mySchoolData.SchoolId,
                    SchoolName = mySchoolName ?? $"School #{mySchoolData.SchoolId}",
                    Score = mySchoolData.Score,
                    Members = mySchoolData.Members
                };
            }
        }

        return new SchoolLeaderboardResponseDto
        {
            Period = period,
            Items = items,
            MySchool = mySchool,
            NextCursor = nextCursor
        };
    }

    private IQueryable<UserProfile> ApplySchool(IQueryable<UserProfile> query, UserProfile me, ref LeaderboardContextDto? context)
    {
        if (me.SchoolId is null)
            return query.Where(_ => false);

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
            return query.Where(_ => false);

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
            query = query.Where(u => u.SchoolId == me.SchoolId);
        else if (scope == "faculty" && me.FacultyId is not null)
            query = query.Where(u => u.FacultyId == me.FacultyId);
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
            query = query.Where(u => u.SchoolId == me.SchoolId);
        else if (scope == "faculty" && me.FacultyId is not null)
            query = query.Where(u => u.FacultyId == me.FacultyId);
        else if (scope == "friends")
        {
            var followees = _db.UserFriends.Where(f => f.UserId == userId).Select(f => f.FriendId);
            query = query.Where(u => u.UserId == userId || followees.Contains(u.UserId));
        }

        // Higher score OR tie but lower ID wins
        var higher = await query.CountAsync(u =>
            ScoreSelector.ScoreOf(u, period) > myScore ||
            (ScoreSelector.ScoreOf(u, period) == myScore && int.Parse(u.UserId) < myId));

        return higher + 1;
    }
}
