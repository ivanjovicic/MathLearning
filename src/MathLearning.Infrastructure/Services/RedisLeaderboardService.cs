using StackExchange.Redis;
using System.Text.Json;
using MathLearning.Application.DTOs.Leaderboard;

namespace MathLearning.Infrastructure.Services;

public class RedisLeaderboardService
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

    public async Task<List<LeaderboardEntryDto>> GetLeaderboardAsync(string scope, string period, int limit)
    {
        string key = GetLeaderboardKey(scope, period);
        var entries = await _redisDb.SortedSetRangeByRankWithScoresAsync(key, 0, limit - 1, Order.Descending);

        return entries.Select((entry, index) => new LeaderboardEntryDto
        {
            Rank = index + 1,
            UserId = entry.Element,
            Xp = (int)entry.Score
        }).ToList();
    }

    public async Task<LeaderboardEntryDto?> GetUserRankAsync(string scope, string period, string userId)
    {
        string key = GetLeaderboardKey(scope, period);
        var rank = await _redisDb.SortedSetRankAsync(key, userId, Order.Descending);
        var score = await _redisDb.SortedSetScoreAsync(key, userId);

        if (rank.HasValue && score.HasValue)
        {
            return new LeaderboardEntryDto
            {
                Rank = (int)rank.Value + 1,
                UserId = userId,
                Xp = (int)score.Value
            };
        }

        return null;
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

        return entries.Select((entry, index) => new LeaderboardEntryDto
        {
            Rank = (int)start + index + 1,
            UserId = entry.Element,
            Xp = (int)entry.Score
        }).ToList();
    }

    public async Task<List<LeaderboardEntryDto>> HydrateMetadataAsync(List<LeaderboardEntryDto> entries)
    {
        var userIds = entries.Select(e => e.UserId).ToArray();
        var metadata = await _redisDb.HashGetAsync("user_metadata", userIds.Select(id => (RedisValue)id).ToArray());

        for (int i = 0; i < entries.Count; i++)
        {
            if (metadata[i].HasValue)
            {
                var userMetadata = JsonSerializer.Deserialize<UserMetadataDto>(metadata[i]!);
                entries[i].DisplayName = userMetadata?.DisplayName ?? entries[i].UserId;
                entries[i].Level = userMetadata?.Level ?? 1;
                entries[i].Streak = userMetadata?.Streak ?? 0;
            }
        }

        return entries;
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
            "friends" => "leaderboard:global:{request.Period}", // Friends handled differently
            _ => throw new ArgumentException($"Invalid scope: {request.Scope}")
        };
    }
}