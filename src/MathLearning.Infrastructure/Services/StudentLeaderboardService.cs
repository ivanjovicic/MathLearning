using System.Text.Json;
using MathLearning.Application.DTOs.Cosmetics;
using MathLearning.Application.DTOs.Leaderboard;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services.Leaderboard;
using MathLearning.Infrastructure.Services.Performance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MathLearning.Infrastructure.Services;

/// <summary>
/// Student leaderboard service — fully decoupled from school leaderboard operations.
/// Reads directly from UserProfiles with scope-aware filtering (global/school/faculty/friends).
/// Appearance data is hydrated from UserAppearanceProjection via HybridCacheService.
/// </summary>
public class StudentLeaderboardService : IStudentLeaderboardService
{
    private readonly ApiDbContext _db;
    private readonly ILogger<StudentLeaderboardService> _logger;
    private readonly ICosmeticRewardService? _cosmeticRewardService;
    private readonly HybridCacheService _cache;

    public StudentLeaderboardService(
        ApiDbContext db,
        ILogger<StudentLeaderboardService> logger,
        HybridCacheService cache,
        ICosmeticRewardService? cosmeticRewardService = null)
    {
        _db = db;
        _logger = logger;
        _cache = cache;
        _cosmeticRewardService = cosmeticRewardService;
    }

    public async Task<LeaderboardResponseDto> GetLeaderboardAsync(
        string userId,
        string scope,
        string period,
        int limit,
        string? cursor = null,
        bool includeMe = true,
        CancellationToken ct = default)
    {
        var startedAt = DateTime.UtcNow;
        limit = Math.Clamp(limit, 1, 200);

        var me = await _db.UserProfiles.FindAsync([userId], ct);
        if (me == null)
        {
            throw new InvalidOperationException($"User profile not found: {userId}");
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
            .ToListAsync(ct);

        var hasMore = page.Count > limit;
        if (hasMore)
        {
            page.RemoveAt(page.Count - 1);
        }

        var appearanceMap = await LoadAppearanceMapAsync(page.Select(x => x.UserId), ct);

        var firstRank = 1;
        if (page.Count > 0)
        {
            var first = page[0];
            firstRank = await LeaderboardRankingUtils.ComputeRankAsync(
                _db, scope, period, me, first.Score, int.Parse(first.UserId), userId, ct);
        }

        var items = page.Select((x, i) => new LeaderboardItemDto
        {
            Rank = firstRank + i,
            UserId = x.UserId,
            DisplayName = x.DisplayName ?? x.Username ?? $"User{x.UserId}",
            AvatarUrl = x.AvatarUrl,
            Appearance = appearanceMap.TryGetValue(x.UserId, out var appearance) ? appearance : null,
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
            var myRank = await LeaderboardRankingUtils.ComputeRankAsync(
                _db, scope, period, me, myScore, int.Parse(me.UserId), userId, ct);
            var totalInScope = await LeaderboardRankingUtils.CountScopeAsync(_db, scope, me, userId, ct);

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

        _logger.LogInformation(
            "Student leaderboard query. Scope={Scope} Period={Period} Limit={Limit} Items={Count} IncludeMe={IncludeMe} ElapsedMs={ElapsedMs}",
            scope, period, limit, items.Count, includeMe,
            Math.Round((DateTime.UtcNow - startedAt).TotalMilliseconds, 2));

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

    private async Task<Dictionary<string, AvatarAppearanceDto>> LoadAppearanceMapAsync(
        IEnumerable<string> userIds,
        CancellationToken ct)
    {
        var ids = userIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<string, AvatarAppearanceDto>();
        }

        var results = new Dictionary<string, AvatarAppearanceDto>(StringComparer.Ordinal);
        var missingIds = new List<string>();

        foreach (var uid in ids)
        {
            var cached = await _cache.GetAsync<AvatarAppearanceDto>($"leaderboard:appearance:{uid}", ct);
            if (cached is null)
            {
                missingIds.Add(uid);
            }
            else
            {
                results[uid] = cached;
            }
        }

        if (missingIds.Count > 0)
        {
            var loaded = await _db.UserAppearanceProjections
                .AsNoTracking()
                .Where(x => missingIds.Contains(x.UserId))
                .ToDictionaryAsync(x => x.UserId, MapAppearance, ct);

            foreach (var pair in loaded)
            {
                results[pair.Key] = pair.Value;
                await _cache.SetAsync(
                    $"leaderboard:appearance:{pair.Key}",
                    pair.Value,
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromMinutes(2),
                    ct);
            }
        }

        return results;
    }

    private static AvatarAppearanceDto MapAppearance(UserAppearanceProjection p)
        => new(
            new AvatarConfigDto(
                p.SkinId, p.HairId, p.ClothingId, p.AccessoryId,
                p.EmojiId, p.FrameId, p.BackgroundId, p.EffectId,
                p.LeaderboardDecorationId, p.AvatarVersion),
            p.SkinAssetPath, p.HairAssetPath, p.ClothingAssetPath,
            p.AccessoryAssetPath, p.EmojiAssetPath, p.FrameAssetPath,
            p.BackgroundAssetPath, p.EffectAssetPath, p.LeaderboardDecorationAssetPath);

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
}
