using MathLearning.Core.Services;
using MathLearning.Services;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace MathLearning.Tests.Services;

public class RedisLeaderboardServiceTests
{
    private readonly Mock<IDatabase> _mockRedisDb;
    private readonly RedisLeaderboardService _service;

    public RedisLeaderboardServiceTests()
    {
        var mockConnection = new Mock<IConnectionMultiplexer>();
        _mockRedisDb = new Mock<IDatabase>();
        mockConnection.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_mockRedisDb.Object);

        _service = new RedisLeaderboardService(mockConnection.Object, Mock.Of<ILogger<RedisLeaderboardService>>());
    }

    [Fact]
    public async Task UpdateLeaderboardAsync_ShouldIncrementScore()
    {
        // Arrange
        string scope = "global";
        string period = "weekly";
        string userId = "user_123";
        int xpDelta = 50;
        string key = $"leaderboard:{scope}:{period}";

        // Act
        await _service.UpdateLeaderboardAsync(scope, period, userId, xpDelta);

        // Assert
        _mockRedisDb.Verify(db => db.SortedSetIncrementAsync(key, userId, xpDelta, CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task GetLeaderboardAsync_ShouldReturnEntries()
    {
        // Arrange
        string scope = "global";
        string period = "weekly";
        int limit = 3;
        string key = $"leaderboard:{scope}:{period}";

        _mockRedisDb.Setup(db => db.SortedSetRangeByRankWithScoresAsync(key, 0, limit - 1, Order.Descending, CommandFlags.None))
            .ReturnsAsync(new[]
            {
                new SortedSetEntry("user_1", 1000),
                new SortedSetEntry("user_2", 900),
                new SortedSetEntry("user_3", 800)
            });

        // Act
        var result = await _service.GetLeaderboardAsync(scope, period, limit);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("user_1", result[0].UserId);
        Assert.Equal(1000, result[0].Xp);
    }

    [Fact]
    public async Task GetUserRankAsync_ShouldReturnRankAndScore()
    {
        // Arrange
        string scope = "global";
        string period = "weekly";
        string userId = "user_123";
        string key = $"leaderboard:{scope}:{period}";

        _mockRedisDb.Setup(db => db.SortedSetRankAsync(key, userId, Order.Descending, CommandFlags.None))
            .ReturnsAsync(5);
        _mockRedisDb.Setup(db => db.SortedSetScoreAsync(key, userId, CommandFlags.None))
            .ReturnsAsync(1200);

        // Act
        var result = await _service.GetUserRankAsync(scope, period, userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(6, result!.Rank); // Redis ranks are 0-based
        Assert.Equal(1200, result.Xp);
    }
}