using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Infrastructure.Services.Leaderboard;

/// <summary>
/// Shared, stateless ranking utilities used by both student and school leaderboard services.
/// Extracted to eliminate duplication and allow independent optimization per service.
/// </summary>
public static class LeaderboardRankingUtils
{
    /// <summary>
    /// Computes the rank of a user within a scope/period by counting all users with a higher score.
    /// Uses deterministic tiebreaker: lower user ID wins on equal score.
    /// </summary>
    public static async Task<int> ComputeRankAsync(
        ApiDbContext db,
        string scope,
        string period,
        UserProfile me,
        int myScore,
        int myId,
        string userId,
        CancellationToken ct = default)
    {
        var query = BuildScopeQuery(db, scope, me, userId);
        var higher = await query.CountAsync(u =>
            ScoreSelector.ScoreOf(u, period) > myScore ||
            (ScoreSelector.ScoreOf(u, period) == myScore && int.Parse(u.UserId) < myId), ct);
        return higher + 1;
    }

    /// <summary>
    /// Counts total users in the given scope (for percentile calculation).
    /// </summary>
    public static async Task<int> CountScopeAsync(
        ApiDbContext db,
        string scope,
        UserProfile me,
        string userId,
        CancellationToken ct = default)
    {
        var query = BuildScopeQuery(db, scope, me, userId);
        return await query.CountAsync(ct);
    }

    private static IQueryable<UserProfile> BuildScopeQuery(
        ApiDbContext db,
        string scope,
        UserProfile me,
        string userId)
    {
        var query = db.UserProfiles.AsNoTracking().Where(u => u.LeaderboardOptIn);
        return scope.ToLowerInvariant() switch
        {
            "school" when me.SchoolId is not null => query.Where(u => u.SchoolId == me.SchoolId),
            "faculty" when me.FacultyId is not null => query.Where(u => u.FacultyId == me.FacultyId),
            "friends" => query.Where(u => u.UserId == userId ||
                db.UserFriends.Where(f => f.UserId == userId).Select(f => f.FriendId).Contains(u.UserId)),
            _ => query
        };
    }
}
