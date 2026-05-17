using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Api.Startup;
using MathLearning.Tests.Helpers;
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

    [Fact]
    public async Task Login_WithSeededTestAccount_ReturnsTokensAndIdentity()
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("test", "test123"));

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

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("test", "wrong"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed record LoginRequest(string Username, string Password);
}
