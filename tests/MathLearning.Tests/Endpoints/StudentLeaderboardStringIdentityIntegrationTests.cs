using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Application.DTOs.Leaderboard;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Endpoints;

public sealed class StudentLeaderboardStringIdentityIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> factory;
    private readonly HttpClient client;

    public StudentLeaderboardStringIdentityIntegrationTests(CustomWebApplicationFactory<Program> factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task StudentLeaderboard_UsesStringUserIds_ForOrderingPagingAndMe()
    {
        var ids = new[]
        {
            "0002",
            "2f3f5d64-5eb2-460f-a6c6-ec4f0a965e31",
            "auth-user-alpha",
            "user-zeta"
        };
        await SeedLeaderboardUsersAsync(ids, xp: 500);

        using var firstRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/leaderboard/student?scope=global&period=all_time&limit=2&includeMe=true");
        firstRequest.Headers.Add("X-Test-UserId", "auth-user-alpha");

        using var firstResponse = await client.SendAsync(firstRequest);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var firstPayload = await firstResponse.Content.ReadFromJsonAsync<LeaderboardResponseDto>();
        Assert.NotNull(firstPayload);
        Assert.NotNull(firstPayload!.Me);
        Assert.Equal(3, firstPayload.Me!.Rank);
        Assert.NotNull(firstPayload.NextCursor);
        Assert.Equal(new[] { "0002", "2f3f5d64-5eb2-460f-a6c6-ec4f0a965e31" }, firstPayload.Items.Select(x => x.UserId));

        using var secondRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/leaderboard/student?scope=global&period=all_time&limit=2&includeMe=false&cursor={Uri.EscapeDataString(firstPayload.NextCursor!)}");
        secondRequest.Headers.Add("X-Test-UserId", "auth-user-alpha");

        using var secondResponse = await client.SendAsync(secondRequest);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        var secondPayload = await secondResponse.Content.ReadFromJsonAsync<LeaderboardResponseDto>();
        Assert.NotNull(secondPayload);
        Assert.Null(secondPayload!.Me);

        var combinedIds = firstPayload.Items
            .Concat(secondPayload.Items)
            .Select(x => x.UserId)
            .ToList();

        Assert.Equal(ids.OrderBy(x => x, StringComparer.Ordinal), combinedIds);
        Assert.Equal(combinedIds.Count, combinedIds.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task StudentLeaderboard_MismatchedCursorContext_ReturnsBadRequest()
    {
        await SeedLeaderboardUsersAsync(
        [
            "0002",
            "auth-user-alpha"
        ], xp: 500);

        using var firstRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/leaderboard/student?scope=global&period=week&limit=1&includeMe=false");
        firstRequest.Headers.Add("X-Test-UserId", "auth-user-alpha");

        using var firstResponse = await client.SendAsync(firstRequest);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var firstPayload = await firstResponse.Content.ReadFromJsonAsync<LeaderboardResponseDto>();
        Assert.NotNull(firstPayload);
        Assert.NotNull(firstPayload!.NextCursor);

        using var mismatchRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/leaderboard/student?scope=global&period=month&limit=1&includeMe=false&cursor={Uri.EscapeDataString(firstPayload.NextCursor!)}");
        mismatchRequest.Headers.Add("X-Test-UserId", "auth-user-alpha");

        using var mismatchResponse = await client.SendAsync(mismatchRequest);
        Assert.Equal(HttpStatusCode.BadRequest, mismatchResponse.StatusCode);

        using var payload = JsonDocument.Parse(await mismatchResponse.Content.ReadAsStringAsync());
        Assert.Equal("cursor_context_mismatch", payload.RootElement.GetProperty("errorCode").GetString());
    }

    private async Task SeedLeaderboardUsersAsync(IEnumerable<string> userIds, int xp)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        foreach (var existing in db.UserProfiles)
        {
            existing.LeaderboardOptIn = false;
        }

        foreach (var userId in userIds)
        {
            if (await userManager.FindByIdAsync(userId) is null)
            {
                await userManager.CreateAsync(new IdentityUser
                {
                    Id = userId,
                    UserName = userId,
                    Email = $"{userId}@example.test"
                });
            }

            var profile = await db.UserProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
            if (profile is null)
            {
                profile = new UserProfile
                {
                    UserId = userId,
                    Username = userId,
                    DisplayName = userId,
                    CreatedAt = DateTime.UtcNow
                };
                db.UserProfiles.Add(profile);
            }

            profile.Xp = xp;
            profile.WeeklyXp = xp;
            profile.MonthlyXp = xp;
            profile.DailyXp = xp;
            profile.Level = 10;
            profile.Streak = 7;
            profile.LeaderboardOptIn = true;
            profile.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
    }
}
