using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Api.Startup;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MathLearning.Tests.Endpoints;

public sealed class AuthDevSeedLoginTests :
    IClassFixture<CustomWebApplicationFactory<Program>>,
    IAsyncLifetime
{
    private readonly CustomWebApplicationFactory<Program> factory;
    private readonly HttpClient client;

    public AuthDevSeedLoginTests(CustomWebApplicationFactory<Program> factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var seeder = ActivatorUtilities.CreateInstance<TestAccountSeeder>(scope.ServiceProvider);
        await seeder.SeedAsync(environment);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("/auth/login")]
    [InlineData("/api/auth/login")]
    public async Task Login_WithSeededTestAccount_ReturnsTokensAndIdentity(string path)
    {
        var response = await client.PostAsJsonAsync(
            path,
            new LoginRequest("test", "test-passphrase-2026!"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(payload.TryGetProperty("accessToken", out var accessTokenElement));
        Assert.False(string.IsNullOrWhiteSpace(accessTokenElement.GetString()));
        Assert.True(payload.TryGetProperty("refreshToken", out var refreshTokenElement));
        Assert.False(string.IsNullOrWhiteSpace(refreshTokenElement.GetString()));
        Assert.True(payload.TryGetProperty("userId", out var userIdElement));
        Assert.False(string.IsNullOrWhiteSpace(userIdElement.GetString()));
        Assert.True(payload.TryGetProperty("username", out var usernameElement));
        Assert.Equal("test", usernameElement.GetString());
    }

    [Theory]
    [InlineData("/auth/login")]
    [InlineData("/api/auth/login")]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized(string path)
    {
        var response = await client.PostAsJsonAsync(
            path,
            new LoginRequest("test", "wrong"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_FiveFailures_LockAccountAndBlockCorrectPassword()
    {
        const string password = "lockout-passphrase-2026!";
        var username = $"lockout-{Guid.NewGuid():N}";
        var email = $"{username}@mathlearning.local";

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = new IdentityUser
            {
                UserName = username,
                Email = email,
                EmailConfirmed = true,
                LockoutEnabled = true
            };

            var createResult = await userManager.CreateAsync(user, password);
            Assert.True(createResult.Succeeded);
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failed = await client.PostAsJsonAsync(
                "/auth/login",
                new LoginRequest(username, "wrong-password"));

            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = await userManager.FindByNameAsync(username);
            Assert.NotNull(user);
            Assert.True(await userManager.IsLockedOutAsync(user!));
        }

        var lockedResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(username, password));

        Assert.Equal(HttpStatusCode.Unauthorized, lockedResponse.StatusCode);
    }

    private sealed record LoginRequest(string Username, string Password);
}
