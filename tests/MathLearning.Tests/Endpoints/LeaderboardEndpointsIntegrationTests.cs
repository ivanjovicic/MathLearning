using System.Net;
using System.Net.Http.Json;
using MathLearning.Api;
using MathLearning.Application.DTOs.Leaderboard;
using MathLearning.Tests.Helpers;
using Xunit;

namespace MathLearning.Tests.Endpoints;

public class LeaderboardEndpointsIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public LeaderboardEndpointsIntegrationTests(CustomWebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetLeaderboard_ReturnsLeaderboardWithCurrentUser()
    {
        // Arrange
        string scope = "global";
        string period = "all_time";
        int limit = 10;
        string url = $"/api/leaderboard?scope={scope}&period={period}&limit={limit}&includeMe=true";

        // Act
        var response = await _client.GetAsync(url);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<LeaderboardResponseDto>();
        Assert.NotNull(result);
        Assert.NotEmpty(result.Items);
        Assert.NotNull(result.Me);
    }

    [Fact]
    public async Task GetSchoolLeaderboard_ReturnsAggregateLeaderboard()
    {
        // Arrange
        string period = "week";
        int limit = 5;
        string url = $"/api/leaderboard/schools?period={period}&limit={limit}";

        // Act
        var response = await _client.GetAsync(url);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<MathLearning.Application.DTOs.Leaderboard.SchoolLeaderboardResponseDto>();
        Assert.NotNull(result);
        Assert.NotEmpty(result.Items);
    }

    [Fact]
    public async Task GetFriendsLeaderboard_ReturnsFriendsOnly()
    {
        // Arrange
        string scope = "friends";
        string period = "weekly";
        int limit = 10;
        string url = $"/api/leaderboard/friends?scope={scope}&period={period}&limit={limit}";

        // Act
        var response = await _client.GetAsync(url);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<LeaderboardResponseDto>();
        Assert.NotNull(result);
        Assert.NotEmpty(result.Items);
        Assert.NotNull(result.Me);
    }

    [Fact]
    public async Task GetSchoolLeaderboardDetail_ReturnsSchoolBreakdown()
    {
        var response = await _client.GetAsync("/api/leaderboard/schools/1?period=week&neighbors=2");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SchoolLeaderboardDetailDto>();
        Assert.NotNull(result);
        Assert.Equal(1, result.School.SchoolId);
        Assert.NotEmpty(result.NearbySchools);
    }

    [Fact]
    public async Task GetSchoolLeaderboardHistory_ReturnsSnapshots()
    {
        var response = await _client.GetAsync("/api/leaderboard/schools/history/1?period=week&take=10");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SchoolLeaderboardHistoryResponseDto>();
        Assert.NotNull(result);
        Assert.Equal(1, result.SchoolId);
        Assert.NotEmpty(result.Points);
    }
}
