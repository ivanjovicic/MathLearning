using System.Net;
using System.Text;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Idempotency;

/// <summary>
/// U2 regression tests: mobile-facing mutations must scope writes to the authenticated user.
/// </summary>
public sealed class MutationUserScopeIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;

    public MutationUserScopeIntegrationTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SettingsPatch_OtherUserRoute_ReturnsForbidden()
    {
        var ownerId = NewUserId("settings-owner");
        var otherId = NewUserId("settings-other");
        await EnsureUserAsync(ownerId);
        await EnsureUserAsync(otherId);

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/users/{otherId}/settings")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { languageCode = "en" }),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add("X-Test-UserId", ownerId);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ProfilePut_UpdatesOnlyAuthenticatedUser()
    {
        var ownerId = NewUserId("profile-owner");
        var otherId = NewUserId("profile-other");
        await EnsureUserAsync(ownerId, displayName: "Owner Before");
        await EnsureUserAsync(otherId, displayName: "Other Unchanged");

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/users/profile")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { displayName = "Owner After" }),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add("X-Test-UserId", ownerId);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var owner = await db.UserProfiles.SingleAsync(x => x.UserId == ownerId);
        var other = await db.UserProfiles.SingleAsync(x => x.UserId == otherId);
        Assert.Equal("Owner After", owner.DisplayName);
        Assert.Equal("Other Unchanged", other.DisplayName);
    }

    [Fact]
    public async Task ProgressSync_PersistsAuthenticatedUserDailyStats()
    {
        var userId = NewUserId("progress-sync");
        await EnsureUserAsync(userId);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/progress/sync")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    completed = true,
                    day = DateOnly.FromDateTime(DateTime.UtcNow).ToString("O"),
                    userId = "must-not-be-used"
                }),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add("X-Test-UserId", userId);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var stat = await db.UserDailyStats.SingleAsync(x => x.UserId == userId);
        Assert.True(stat.Completed);
        Assert.Equal(0, await db.UserDailyStats.CountAsync(x => x.UserId == "must-not-be-used"));
    }

    private static string NewUserId(string suffix) => $"u2-scope-{suffix}-{Guid.NewGuid():N}";

    private async Task EnsureUserAsync(string userId, string displayName = "Test User")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        if (!await db.Users.AnyAsync(x => x.Id == userId))
        {
            db.Users.Add(new IdentityUser
            {
                Id = userId,
                UserName = userId,
                Email = $"{userId}@example.test"
            });
        }

        if (!await db.UserProfiles.AnyAsync(x => x.UserId == userId))
        {
            db.UserProfiles.Add(new UserProfile
            {
                UserId = userId,
                Username = userId,
                DisplayName = displayName,
                Coins = 0,
                Level = 1,
                Xp = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }
}
