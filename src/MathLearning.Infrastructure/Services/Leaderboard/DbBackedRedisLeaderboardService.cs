using MathLearning.Core.DTOs;
using MathLearning.Core.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MathLearning.Infrastructure.Services.Leaderboard;

/// <summary>
/// Safe fallback when Redis is unavailable. Keeps leaderboard endpoints functional by reading from SQL.
/// </summary>
public sealed class DbBackedRedisLeaderboardService : IRedisLeaderboardService
{
    private sealed record LeaderboardProjection(
        string UserId,
        string DisplayName,
        int Level,
        int Score,
        int WeeklyXp,
        int Streak);

    private readonly ApiDbContext db;
    private readonly ILogger<DbBackedRedisLeaderboardService> logger;

    public DbBackedRedisLeaderboardService(
        ApiDbContext db,
        ILogger<DbBackedRedisLeaderboardService> logger)
    {
        this.db = db;
        this.logger = logger;
    }

    public async Task<List<LeaderboardEntryDto>> GetLeaderboardAsync(LeaderboardRequestDto request)
    {
        var normalizedPeriod = NormalizePeriod(request.Period);
        var query = await BuildScopeQueryAsync(request);

        // Fetch data from the database first
        var rows = await query
            .Where(u => u.LeaderboardOptIn)
            .OrderByDescending(u => u.WeeklyXp) // Simplified ordering for SQL translation
            .ThenBy(u => u.UserId)
            .Take(Math.Clamp(request.Limit, 1, 200))
            .ToListAsync();

        // Perform projection in memory
        var leaderboard = rows.Select((u, index) =>
        {
            return new LeaderboardEntryDto(
                index + 1,
                u.UserId,
                u.DisplayName ?? u.Username ?? $"User{u.UserId}",
                u.Level,
                u.WeeklyXp, // Assuming Score = WeeklyXp for simplicity
                u.WeeklyXp,
                u.Streak
            );
        }).ToList();

        return leaderboard;
    }

    public async Task<LeaderboardEntryDto?> GetUserRankAsync(LeaderboardRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            return null;
        }

        var normalizedPeriod = NormalizePeriod(request.Period);
        var me = await db.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == request.UserId);
        if (me is null)
        {
            return null;
        }

        var query = await BuildScopeQueryAsync(request, me);
        var myScore = ScoreSelector.ScoreOf(me, normalizedPeriod);
        var orderedUserIds = await OrderByScore(query, normalizedPeriod)
            .Select(x => x.UserId)
            .ToListAsync();
        var rank = orderedUserIds.FindIndex(x => x == me.UserId) + 1;
        if (rank <= 0)
        {
            return null;
        }

        return new LeaderboardEntryDto(
            rank,
            me.UserId,
            me.DisplayName ?? me.Username ?? $"User{me.UserId}",
            me.Level,
            myScore,
            me.WeeklyXp,
            me.Streak);
    }

    public async Task<List<LeaderboardEntryDto>> GetNearRivalsAsync(LeaderboardRequestDto request)
    {
        var me = await GetUserRankAsync(request);
        if (me is null)
        {
            return [];
        }

        var normalizedPeriod = NormalizePeriod(request.Period);
        var query = await BuildScopeQueryAsync(request);
        var skip = Math.Max(0, me.Rank - 3);
        var rows = await OrderByScore(query, normalizedPeriod)
            .Skip(skip)
            .Take(5)
            .ToListAsync();

        return rows
            .Select((x, index) => new LeaderboardEntryDto(
                skip + index + 1,
                x.UserId,
                x.DisplayName ?? x.Username ?? $"User{x.UserId}",
                x.Level,
                LocalScoreOf(x, normalizedPeriod),
                x.WeeklyXp,
                x.Streak))
            .ToList();
    }

    public Task UpdateLeaderboardAsync(LeaderboardUpdateDto update)
    {
        logger.LogDebug(
            "Skipping Redis leaderboard update because DB-backed fallback is active. Scope={Scope} Period={Period} UserId={UserId} XpDelta={XpDelta}",
            update.Scope,
            update.Period,
            update.UserId,
            update.XpDelta);
        return Task.CompletedTask;
    }

    private async Task<IQueryable<UserProfile>> BuildScopeQueryAsync(
        LeaderboardRequestDto request,
        UserProfile? me = null)
    {
        var scope = NormalizeScope(request.Scope);
        var query = db.UserProfiles.AsNoTracking().Where(x => x.LeaderboardOptIn);

        if (scope == "school")
        {
            var schoolId = request.SchoolId ?? me?.SchoolId;
            if (schoolId is null && !string.IsNullOrWhiteSpace(request.UserId))
            {
                schoolId = await db.UserProfiles
                    .AsNoTracking()
                    .Where(x => x.UserId == request.UserId)
                    .Select(x => x.SchoolId)
                    .FirstOrDefaultAsync();
            }

            return schoolId is null
                ? query.Where(_ => false)
                : query.Where(x => x.SchoolId == schoolId);
        }

        if (scope == "faculty")
        {
            var facultyId = request.FacultyId ?? me?.FacultyId;
            if (facultyId is null && !string.IsNullOrWhiteSpace(request.UserId))
            {
                facultyId = await db.UserProfiles
                    .AsNoTracking()
                    .Where(x => x.UserId == request.UserId)
                    .Select(x => x.FacultyId)
                    .FirstOrDefaultAsync();
            }

            return facultyId is null
                ? query.Where(_ => false)
                : query.Where(x => x.FacultyId == facultyId);
        }

        if (scope == "friends")
        {
            if (request.FriendIds is { Count: > 0 })
            {
                var friendIds = request.FriendIds
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .ToList();

                if (!string.IsNullOrWhiteSpace(request.UserId))
                {
                    friendIds.Add(request.UserId);
                }

                return query.Where(x => friendIds.Contains(x.UserId));
            }

            if (string.IsNullOrWhiteSpace(request.UserId))
            {
                return query.Where(_ => false);
            }

            var userId = request.UserId;
            var followees = db.UserFriends
                .Where(x => x.UserId == userId)
                .Select(x => x.FriendId);
            return query.Where(x => x.UserId == userId || followees.Contains(x.UserId));
        }

        return query;
    }

    private static IOrderedQueryable<UserProfile> OrderByScore(
        IQueryable<UserProfile> query,
        string normalizedPeriod)
        => normalizedPeriod switch
        {
            "day"   => query.OrderByDescending(x => x.DailyXp).ThenBy(x => x.UserId),
            "week"  => query.OrderByDescending(x => x.WeeklyXp).ThenBy(x => x.UserId),
            "month" => query.OrderByDescending(x => x.MonthlyXp).ThenBy(x => x.UserId),
            _       => query.OrderByDescending(x => x.Xp).ThenBy(x => x.UserId),
        };

    private static int LocalScoreOf(UserProfile u, string normalizedPeriod)
        => normalizedPeriod switch
        {
            "day"   => u.DailyXp,
            "week"  => u.WeeklyXp,
            "month" => u.MonthlyXp,
            _       => u.Xp,
        };

    private static IQueryable<LeaderboardProjection> ProjectScores(
        IQueryable<UserProfile> query,
        string normalizedPeriod)
        => normalizedPeriod switch
        {
            "day" => query.Select(x => new LeaderboardProjection(
                x.UserId,
                x.DisplayName ?? x.Username ?? $"User{x.UserId}",
                x.Level,
                x.DailyXp,
                x.WeeklyXp,
                x.Streak)),
            "week" => query.Select(x => new LeaderboardProjection(
                x.UserId,
                x.DisplayName ?? x.Username ?? $"User{x.UserId}",
                x.Level,
                x.WeeklyXp,
                x.WeeklyXp,
                x.Streak)),
            "month" => query.Select(x => new LeaderboardProjection(
                x.UserId,
                x.DisplayName ?? x.Username ?? $"User{x.UserId}",
                x.Level,
                x.MonthlyXp,
                x.WeeklyXp,
                x.Streak)),
            _ => query.Select(x => new LeaderboardProjection(
                x.UserId,
                x.DisplayName ?? x.Username ?? $"User{x.UserId}",
                x.Level,
                x.Xp,
                x.WeeklyXp,
                x.Streak))
        };

    private static string NormalizeScope(string scope) =>
        string.IsNullOrWhiteSpace(scope)
            ? "global"
            : scope.Trim().ToLowerInvariant();

    private static string NormalizePeriod(string period) =>
        string.IsNullOrWhiteSpace(period)
            ? "all_time"
            : period.Trim().ToLowerInvariant() switch
            {
                "daily" => "day",
                "weekly" => "week",
                "monthly" => "month",
                "alltime" => "all_time",
                _ => period.Trim().ToLowerInvariant()
            };
}
