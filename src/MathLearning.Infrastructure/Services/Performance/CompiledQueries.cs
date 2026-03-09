using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Infrastructure.Services.Performance;

public static class CompiledQueries
{
    public sealed record LeaderboardRow(
        string UserId,
        string? DisplayName,
        string? Username,
        int Level,
        int Xp,
        int WeeklyXp,
        int MonthlyXp,
        int DailyXp,
        int Streak,
        string? AvatarUrl);

    private static readonly Func<ApiDbContext, string, IAsyncEnumerable<UserProfile>> UserProfileByIdQuery =
        EF.CompileAsyncQuery(
            (ApiDbContext db, string userId) => db.UserProfiles
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Take(1));

    private static readonly Func<ApiDbContext, string, IAsyncEnumerable<UserSettings>> UserSettingsByUserIdQuery =
        EF.CompileAsyncQuery(
            (ApiDbContext db, string userId) => db.UserSettings
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Take(1));

    private static readonly Func<ApiDbContext, int, IAsyncEnumerable<LeaderboardRow>> GlobalLeaderboardAllTimeQuery =
        EF.CompileAsyncQuery(
            (ApiDbContext db, int limit) => db.UserProfiles
                .AsNoTracking()
                .Where(x => x.LeaderboardOptIn)
                .OrderByDescending(x => x.Xp)
                .ThenBy(x => x.UserId)
                .Select(x => new LeaderboardRow(
                    x.UserId,
                    x.DisplayName,
                    x.Username,
                    x.Level,
                    x.Xp,
                    x.WeeklyXp,
                    x.MonthlyXp,
                    x.DailyXp,
                    x.Streak,
                    x.AvatarUrl))
                .Take(limit));

    private static readonly Func<ApiDbContext, int, IAsyncEnumerable<LeaderboardRow>> GlobalLeaderboardWeeklyQuery =
        EF.CompileAsyncQuery(
            (ApiDbContext db, int limit) => db.UserProfiles
                .AsNoTracking()
                .Where(x => x.LeaderboardOptIn)
                .OrderByDescending(x => x.WeeklyXp)
                .ThenByDescending(x => x.Xp)
                .ThenBy(x => x.UserId)
                .Select(x => new LeaderboardRow(
                    x.UserId,
                    x.DisplayName,
                    x.Username,
                    x.Level,
                    x.Xp,
                    x.WeeklyXp,
                    x.MonthlyXp,
                    x.DailyXp,
                    x.Streak,
                    x.AvatarUrl))
                .Take(limit));

    private static readonly Func<ApiDbContext, string, DateTime, DateTime, IAsyncEnumerable<SchoolScoreAggregate>> SchoolAggregatesByPeriodQuery =
        EF.CompileAsyncQuery(
            (ApiDbContext db, string period, DateTime periodStartUtc, DateTime cutoffUtc) => db.SchoolScoreAggregates
                .AsNoTracking()
                .Where(x =>
                    x.Period == period &&
                    x.PeriodStartUtc == periodStartUtc &&
                    x.UpdatedAtUtc >= cutoffUtc));

    public static IAsyncEnumerable<UserProfile> GetUserProfileById(ApiDbContext db, string userId)
        => UserProfileByIdQuery(db, userId);

    public static IAsyncEnumerable<UserSettings> GetUserSettingsByUserId(ApiDbContext db, string userId)
        => UserSettingsByUserIdQuery(db, userId);

    public static IAsyncEnumerable<LeaderboardRow> GetGlobalLeaderboard(ApiDbContext db, string range, int limit)
        => string.Equals(range, "weekly", StringComparison.OrdinalIgnoreCase)
            ? GlobalLeaderboardWeeklyQuery(db, limit)
            : GlobalLeaderboardAllTimeQuery(db, limit);

    public static IAsyncEnumerable<SchoolScoreAggregate> GetFreshSchoolAggregates(ApiDbContext db, string period, DateTime periodStartUtc, DateTime cutoffUtc)
        => SchoolAggregatesByPeriodQuery(db, period, periodStartUtc, cutoffUtc);
}
