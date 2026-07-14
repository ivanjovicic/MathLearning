using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Api.Startup;
using MathLearning.Application.DTOs.Auth;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace MathLearning.Tests.Endpoints;

public sealed class AuthRefreshPostgresConcurrencyTests
{
    [Fact]
    [Trait("Category", "PostgresProvider")]
    public async Task ConcurrentRefreshRequests_Postgres_AllowsExactlyOneRotation()
    {
        if (!IsValidationRequired())
        {
            return;
        }

        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var factory = new PostgresAuthRefreshWebApplicationFactory(database);

        using var client = factory.CreateClient();
        await factory.SeedTestAccountAsync();

        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("test", "test123"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var loginJson = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var userId = loginJson.RootElement.GetProperty("userId").GetString();
        var originalRefreshToken = loginJson.RootElement.GetProperty("refreshToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(userId));
        Assert.False(string.IsNullOrWhiteSpace(originalRefreshToken));

        factory.RefreshCoordinator.Enable();

        var firstRequest = client.PostAsJsonAsync(
            "/auth/refresh",
            new TokenRequest(originalRefreshToken!));
        var secondRequest = client.PostAsJsonAsync(
            "/auth/refresh",
            new TokenRequest(originalRefreshToken!));

        await factory.RefreshCoordinator.WaitUntilBothWritersArriveAsync();
        await Task.WhenAny(firstRequest, secondRequest).WaitAsync(TimeSpan.FromSeconds(15));
        factory.RefreshCoordinator.ReleaseSecondWriter();

        var responses = await Task.WhenAll(firstRequest, secondRequest);

        Assert.Equal(1, responses.Count(x => x.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(x => x.StatusCode == HttpStatusCode.Unauthorized));

        var unauthorized = responses.Single(x => x.StatusCode == HttpStatusCode.Unauthorized);
        using (var unauthorizedJson = JsonDocument.Parse(await unauthorized.Content.ReadAsStringAsync()))
        {
            Assert.Equal(
                "Invalid or expired refresh token",
                unauthorizedJson.RootElement.GetProperty("error").GetString());
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var tokens = await db.RefreshTokens
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();

        Assert.Equal(2, tokens.Count);
        Assert.Single(tokens.Where(x => x.RevokedAt is null));
        Assert.Contains(tokens, x => x.Token == originalRefreshToken && x.RevokedAt is not null);
        Assert.DoesNotContain(tokens, x => x.Token == originalRefreshToken && x.RevokedAt is null);
    }

    private static bool IsValidationRequired()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable("POSTGRES_PROVIDER_TESTS_REQUIRED"),
            "1",
            StringComparison.Ordinal);
    }
}

public sealed class PostgresAuthRefreshWebApplicationFactory : PostgresWebApplicationFactory<Program>
{
    public PostgresAuthRefreshWebApplicationFactory(PostgresTestDatabase database)
        : base(database)
    {
    }

    public RefreshRotationSaveCoordinator RefreshCoordinator { get; } = new();

    protected override void ConfigureApiDb(DbContextOptionsBuilder options)
    {
        options.AddInterceptors(RefreshCoordinator);
    }

    public async Task SeedTestAccountAsync()
    {
        using var scope = Services.CreateScope();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var seeder = ActivatorUtilities.CreateInstance<TestAccountSeeder>(scope.ServiceProvider);
        await seeder.SeedAsync(environment);
    }
}
