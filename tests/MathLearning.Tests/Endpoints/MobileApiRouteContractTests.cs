using System.Net;
using System.Net.Http.Json;
using MathLearning.Api;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Endpoints;

public sealed class MobileApiRouteContractTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public MobileApiRouteContractTests(CustomWebApplicationFactory<Program> factory)
    {
        SeedCurrentUserProfile(factory);
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("GET", "/api/quiz/questions")]
    [InlineData("POST", "/api/quiz/answer")]
    [InlineData("GET", "/api/progress/overview")]
    [InlineData("GET", "/api/progress/topics")]
    [InlineData("GET", "/api/adaptive/path")]
    [InlineData("GET", "/api/adaptive/reviews/due")]
    [InlineData("GET", "/api/adaptive/recommendations")]
    [InlineData("GET", "/api/leaderboard/rivals")]
    [InlineData("GET", "/api/users/profile")]
    [InlineData("GET", "/api/user/coins")]
    [InlineData("POST", "/api/daily-run/chest/claim")]
    public async Task MobileRoutes_ArePresent_And_DoNotReturn404(string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST")
        {
            request.Content = JsonContent.Create(new { });
        }

        var response = await _client.SendAsync(request);

        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/analytics/mastery")]
    [InlineData("/api/chase/test")]
    public async Task UnsupportedMobileRoutes_AreAbsent(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/adaptive/session/start")]
    [InlineData("/api/adaptive/session/answer")]
    public async Task UnsupportedMobileRoutes_ArePostOnly(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    private static void SeedCurrentUserProfile(CustomWebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

        if (!db.Users.Any(x => x.Id == "test-user"))
        {
            db.Users.Add(new IdentityUser
            {
                Id = "test-user",
                UserName = "test-user",
                Email = "test-user@example.test"
            });
        }

        if (!db.UserProfiles.Any(x => x.UserId == "test-user"))
        {
            db.UserProfiles.Add(new UserProfile
            {
                UserId = "test-user",
                Username = "test-user",
                DisplayName = "Test User",
                Coins = 100,
                Level = 1,
                Xp = 0,
                Streak = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        db.SaveChanges();
    }
}
