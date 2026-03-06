using StackExchange.Redis;
using System.Text.Json;
using MathLearning.Core.DTOs;
using Microsoft.Extensions.Logging;
using MathLearning.Core.Services;

namespace MathLearning.Services;

public class RedisLeaderboardService : IRedisLeaderboardService
{
    private readonly IDatabase _redisDb;
    private readonly ILogger<RedisLeaderboardService> _logger;

    public RedisLeaderboardService(IConnectionMultiplexer redis, ILogger<RedisLeaderboardService> logger)
    {
        _redisDb = redis.GetDatabase();
        _logger = logger;
    }

    public async Task UpdateLeaderboardAsync(string scope, string period, string userId, int xpDelta)
    {
        string key = GetLeaderboardKey(scope, period);
        await _redisDb.SortedSetIncrementAsync(key, userId, xpDelta);
        _logger.LogInformation("Updated leaderboard {Key} for user {UserId} with XP delta {XpDelta}", key, userId, xpDelta);
    }

    // New DTO-based API (implements IRedisLeaderboardService)
    public Task UpdateLeaderboardAsync(LeaderboardUpdateDto update)
    {
        return UpdateLeaderboardAsync(update.Scope, update.Period, update.UserId, update.XpDelta);
    }

    public async Task<List<LeaderboardEntryDto>> GetLeaderboardAsync(string scope, string period, int limit)
    {
        string key = GetLeaderboardKey(scope, period);
        var entries = await _redisDb.SortedSetRangeByRankWithScoresAsync(key, 0, limit - 1, Order.Descending);

        return entries.Select((entry, index) => new LeaderboardEntryDto(
            index + 1,
            entry.Element,
            "Unknown", // Placeholder for DisplayName
            0,          // Placeholder for Level
            (int)entry.Score,
            0,          // Placeholder for WeeklyXp
            0           // Placeholder for Streak
        )).ToList();
    }

    // DTO-based wrapper
    public Task<List<LeaderboardEntryDto>> GetLeaderboardAsync(LeaderboardRequestDto request)
    {
        // For friends scope, special handling may be required by callers (friend list)
        return GetLeaderboardAsync(request.Scope, request.Period, request.Limit);
    }

    public async Task<LeaderboardEntryDto?> GetUserRankAsync(string scope, string period, string userId)
    {
        string key = GetLeaderboardKey(scope, period);
        var rank = await _redisDb.SortedSetRankAsync(key, userId, Order.Descending);
        var score = await _redisDb.SortedSetScoreAsync(key, userId);

        if (rank.HasValue && score.HasValue)
        {
            return new LeaderboardEntryDto(
                (int)rank.Value + 1,
                userId,
                "Unknown", // Placeholder for DisplayName
                0,          // Placeholder for Level
                (int)score.Value,
                0,          // Placeholder for WeeklyXp
                0           // Placeholder for Streak
            );
        }

        return null;
    }

    // DTO-based wrapper
    public Task<LeaderboardEntryDto?> GetUserRankAsync(LeaderboardRequestDto request)
    {
        if (request.UserId is null)
            return Task.FromResult<LeaderboardEntryDto?>(null);

        return GetUserRankAsync(request.Scope, request.Period, request.UserId);
    }

    public async Task<List<LeaderboardEntryDto>> GetNearRivalsAsync(string scope, string period, string userId)
    {
        string key = GetLeaderboardKey(scope, period);
        var rank = await _redisDb.SortedSetRankAsync(key, userId, Order.Descending);

        if (!rank.HasValue)
            return new List<LeaderboardEntryDto>();

        long start = Math.Max(0, rank.Value - 2);
        long end = rank.Value + 2;

        var entries = await _redisDb.SortedSetRangeByRankWithScoresAsync(key, start, end, Order.Descending);

        return entries.Select((entry, index) => new LeaderboardEntryDto(
            (int)start + index + 1,
            entry.Element,
            "Unknown", // Placeholder for DisplayName
            0,          // Placeholder for Level
            (int)entry.Score,
            0,          // Placeholder for WeeklyXp
            0           // Placeholder for Streak
        )).ToList();
    }

    // DTO-based wrapper
    public Task<List<LeaderboardEntryDto>> GetNearRivalsAsync(LeaderboardRequestDto request)
    {
        if (request.UserId is null)
            return Task.FromResult(new List<LeaderboardEntryDto>());

        return GetNearRivalsAsync(request.Scope, request.Period, request.UserId);
    }

    private string GetLeaderboardKey(string scope, string period)
    {
        return $"leaderboard:{scope}:{period}";
    }

    private string GetLeaderboardKey(LeaderboardRequestDto request)
    {
        return request.Scope switch
        {
            "global" => $"leaderboard:global:{request.Period}",
            "school" => $"leaderboard:school:{request.SchoolId}:{request.Period}",
            "faculty" => $"leaderboard:faculty:{request.FacultyId}:{request.Period}",
            "friends" => $"leaderboard:global:{request.Period}", // friends are filtered client-side by friend IDs
            _ => $"leaderboard:{request.Scope}:{request.Period}"
        };
    }
}