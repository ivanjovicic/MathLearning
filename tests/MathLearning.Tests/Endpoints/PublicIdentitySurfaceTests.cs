using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Endpoints;

public sealed class PublicIdentitySurfaceTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;

    public PublicIdentitySurfaceTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SearchUsers_PublicShape_DoesNotExposeProgressOrUsername()
    {
        var viewerUserId = $"viewer-search-{Guid.NewGuid():N}";
        var targetUserId = $"target-search-{Guid.NewGuid():N}";
        var searchToken = Guid.NewGuid().ToString("N");
        await EnsureUserAsync(viewerUserId, "viewer-search", "Viewer Search", 10, 100, 5, 1, 2, 3, 4);
        await EnsureUserAsync(targetUserId, $"alpha-user-{searchToken}", $"Alpha Result {searchToken}", 20, 321, 4, 7, 77, 88, 99);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/users/search?query={searchToken}");
        request.Headers.Add("X-Test-UserId", viewerUserId);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, payload.ValueKind);

        var first = payload.EnumerateArray().Single();
        Assert.Equal(targetUserId, first.GetProperty("userId").GetString());
        Assert.Equal($"Alpha Result {searchToken}", first.GetProperty("displayName").GetString());
        Assert.Equal(4, first.GetProperty("level").GetInt32());
        Assert.False(first.TryGetProperty("username", out _));
        Assert.False(first.TryGetProperty("xp", out _));
        Assert.False(first.TryGetProperty("dailyXp", out _));
        Assert.False(first.TryGetProperty("weeklyXp", out _));
        Assert.False(first.TryGetProperty("monthlyXp", out _));
    }

    [Fact]
    public async Task PublicProfile_ById_DoesNotMirrorPrivateProgress()
    {
        var viewerUserId = $"viewer-profile-{Guid.NewGuid():N}";
        var targetUserId = $"target-profile-{Guid.NewGuid():N}";
        await EnsureUserAsync(viewerUserId, "viewer-profile", "Viewer Profile", 10, 100, 5, 1, 2, 3, 4);
        await EnsureUserAsync(targetUserId, "alpha-profile", "Alpha Profile", 25, 432, 7, 12, 44, 55, 66);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/user/profile/{targetUserId}");
        request.Headers.Add("X-Test-UserId", viewerUserId);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(targetUserId, payload.GetProperty("userId").GetString());
        Assert.Equal("Alpha Profile", payload.GetProperty("displayName").GetString());
        Assert.Equal(7, payload.GetProperty("level").GetInt32());
        Assert.Equal(12, payload.GetProperty("streak").GetInt32());
        Assert.False(payload.TryGetProperty("username", out _));
        Assert.False(payload.TryGetProperty("xp", out _));
        Assert.False(payload.TryGetProperty("dailyXp", out _));
        Assert.False(payload.TryGetProperty("weeklyXp", out _));
        Assert.False(payload.TryGetProperty("monthlyXp", out _));
        Assert.False(payload.TryGetProperty("coins", out _));
        Assert.False(payload.TryGetProperty("schoolName", out _));
        Assert.False(payload.TryGetProperty("facultyName", out _));
    }

    [Fact]
    public async Task Leaderboard_PublicShape_DoesNotExposeLegacyCosmeticMetadata()
    {
        var viewerUserId = "test-user";

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/leaderboard?scope=global&period=all_time&limit=3&includeMe=true");
        request.Headers.Add("X-Test-UserId", viewerUserId);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = payload.GetProperty("items");
        Assert.Equal(JsonValueKind.Array, items.ValueKind);

        var first = items.EnumerateArray().First();
        Assert.Equal(1, first.GetProperty("rank").GetInt32());
        Assert.Equal("test-user", first.GetProperty("userId").GetString());
        Assert.Equal("Test User", first.GetProperty("displayName").GetString());
        Assert.Equal(1500, first.GetProperty("score").GetInt32());
        Assert.Equal(10, first.GetProperty("streakDays").GetInt32());
        Assert.Equal(5, first.GetProperty("level").GetInt32());
        Assert.False(first.TryGetProperty("avatarFrameId", out _));
        Assert.False(first.TryGetProperty("trailId", out _));
        Assert.False(first.TryGetProperty("avatarGearId", out _));
        Assert.False(first.TryGetProperty("answerEffectId", out _));
        Assert.False(first.TryGetProperty("profileBackgroundId", out _));
        Assert.False(first.TryGetProperty("recentRareUnlocks", out _));
        Assert.False(first.TryGetProperty("cosmeticLoadout", out _));
    }

    private async Task EnsureUserAsync(
        string userId,
        string username,
        string displayName,
        int coins,
        int xp,
        int level,
        int streak,
        int dailyXp,
        int weeklyXp,
        int monthlyXp)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        var identity = await db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (identity is null)
        {
            db.Users.Add(new IdentityUser
            {
                Id = userId,
                UserName = username,
                Email = $"{userId}@example.test"
            });
        }

        var profile = await db.UserProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
        if (profile is null)
        {
            profile = new UserProfile
            {
                UserId = userId,
                Username = username,
                DisplayName = displayName,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.UserProfiles.Add(profile);
        }

        profile.DisplayName = displayName;
        profile.Username = username;
        profile.Coins = coins;
        profile.Xp = xp;
        profile.Level = level;
        profile.Streak = streak;
        profile.DailyXp = dailyXp;
        profile.WeeklyXp = weeklyXp;
        profile.MonthlyXp = monthlyXp;
        profile.SchoolName = "Privacy High";
        profile.FacultyName = "Privacy Faculty";
        profile.AvatarUrl = $"/avatars/{userId}.png";
        profile.LeaderboardOptIn = true;
        profile.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
    }
}
