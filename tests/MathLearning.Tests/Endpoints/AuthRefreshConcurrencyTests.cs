using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Api.Middleware;
using MathLearning.Api.Startup;
using MathLearning.Application.DTOs.Auth;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace MathLearning.Tests.Endpoints;

public sealed class AuthRefreshConcurrencyTests
{
    private const string SecurityStamp = "refresh-test-security-stamp";

    [Fact]
    public async Task ConcurrentRefreshRotations_OnlyOneSaveSucceeds()
    {
        var dbName = $"refresh-race-{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<ApiDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var initialToken = RefreshTokenService.CreateRefreshToken("user-1", SecurityStamp, "test-device", "127.0.0.1");

        await using (var setup = new ApiDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.RefreshTokens.Add(initialToken);
            await setup.SaveChangesAsync();
        }

        var results = await Task.WhenAll(
            RotateRefreshTokenAsync(options, initialToken.Token),
            RotateRefreshTokenAsync(options, initialToken.Token));

        Assert.Equal(1, results.Count(result => result));
        Assert.Equal(1, results.Count(result => !result));

        await using var verify = new ApiDbContext(options);
        var tokens = await verify.RefreshTokens
            .Where(t => t.UserId == "user-1")
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();

        Assert.Equal(2, tokens.Count);
        Assert.Single(tokens.Where(t => t.RevokedAt is null));
        Assert.Contains(tokens, t => t.Token == initialToken.Token && t.RevokedAt is not null);
    }

    [Fact]
    public async Task ConcurrentRefreshRotations_WithSqliteRelationalProvider_OnlyOneSaveSucceeds()
    {
        var connectionString = $"Data Source=file:refresh-race-{Guid.NewGuid():N}?mode=memory&cache=shared";
        await using var keeper = new SqliteConnection(connectionString);
        await keeper.OpenAsync();

        var options = new DbContextOptionsBuilder<RefreshTokenTestDbContext>()
            .UseSqlite(connectionString)
            .Options;

        var initialToken = RefreshTokenService.CreateRefreshToken("user-1", SecurityStamp, "test-device", "127.0.0.1");

        await using (var setup = new RefreshTokenTestDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.RefreshTokens.Add(initialToken);
            await setup.SaveChangesAsync();
        }

        var results = await Task.WhenAll(
            RotateRefreshTokenAsync(options, initialToken.Token),
            RotateRefreshTokenAsync(options, initialToken.Token));

        Assert.Equal(1, results.Count(result => result));
        Assert.Equal(1, results.Count(result => !result));

        await using var verify = new RefreshTokenTestDbContext(options);
        var tokens = await verify.RefreshTokens
            .Where(t => t.UserId == "user-1")
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();

        Assert.Equal(2, tokens.Count);
        Assert.Single(tokens.Where(t => t.RevokedAt is null));
        Assert.Contains(tokens, t => t.Token == initialToken.Token && t.RevokedAt is not null);
    }

    [Fact]
    public async Task ReusingRotatedRefreshToken_DoesNotMintAThirdToken()
    {
        var dbName = $"refresh-reuse-{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<ApiDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var initialToken = RefreshTokenService.CreateRefreshToken("user-1", SecurityStamp, "test-device", "127.0.0.1");

        await using (var setup = new ApiDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.RefreshTokens.Add(initialToken);
            await setup.SaveChangesAsync();
        }

        var firstRotation = await RotateRefreshTokenAsync(options, initialToken.Token);
        var secondRotation = await RotateRefreshTokenAsync(options, initialToken.Token);

        Assert.True(firstRotation);
        Assert.False(secondRotation);

        await using var verify = new ApiDbContext(options);
        var tokens = await verify.RefreshTokens
            .Where(t => t.UserId == "user-1")
            .ToListAsync();

        Assert.Equal(2, tokens.Count);
        Assert.Single(tokens.Where(t => t.RevokedAt is null));
        Assert.Contains(tokens, t => t.Token == initialToken.Token && t.RevokedAt is not null);
    }

    private static async Task<bool> RotateRefreshTokenAsync(DbContextOptions<ApiDbContext> options, string refreshTokenValue)
    {
        await using var db = new ApiDbContext(options);
        var refreshToken = await db.RefreshTokens.SingleAsync(t => t.Token == refreshTokenValue);

        if (!RefreshTokenService.ValidateRefreshToken(refreshToken))
        {
            return false;
        }

        RefreshTokenService.RevokeToken(refreshToken);

        db.RefreshTokens.Add(RefreshTokenService.CreateRefreshToken(
            refreshToken.UserId,
            SecurityStamp,
            refreshToken.Device,
            refreshToken.IpAddress,
            expiryDays: 14));

        try
        {
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    private static async Task<bool> RotateRefreshTokenAsync(DbContextOptions<RefreshTokenTestDbContext> options, string refreshTokenValue)
    {
        await using var db = new RefreshTokenTestDbContext(options);
        var refreshToken = await db.RefreshTokens.SingleAsync(t => t.Token == refreshTokenValue);

        if (!RefreshTokenService.ValidateRefreshToken(refreshToken))
        {
            return false;
        }

        RefreshTokenService.RevokeToken(refreshToken);

        db.RefreshTokens.Add(RefreshTokenService.CreateRefreshToken(
            refreshToken.UserId,
            SecurityStamp,
            refreshToken.Device,
            refreshToken.IpAddress,
            expiryDays: 14));

        try
        {
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    private sealed class RefreshTokenTestDbContext : DbContext
    {
        public RefreshTokenTestDbContext(DbContextOptions<RefreshTokenTestDbContext> options)
            : base(options)
        {
        }

        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Token).IsRequired().HasMaxLength(64);
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.ExpiresAt).IsRequired();
                entity.Property(e => e.RevokedAt).IsConcurrencyToken();
            });
        }
    }
}

public sealed class AuthRefreshEndpointRegressionTests :
    IClassFixture<CustomWebApplicationFactory<Program>>,
    IAsyncLifetime
{
    private readonly CustomWebApplicationFactory<Program> factory;
    private readonly HttpClient client;

    public AuthRefreshEndpointRegressionTests(CustomWebApplicationFactory<Program> factory)
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
    public async Task Refresh_ReusingRotatedToken_ReturnsUnauthorized()
    {
        var (userId, refreshToken) = await LoginForTokensAsync();

        var first = await client.PostAsJsonAsync("/auth/refresh", new TokenRequest(refreshToken));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var replay = await client.PostAsJsonAsync("/auth/refresh", new TokenRequest(refreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var tokens = await db.RefreshTokens.Where(t => t.UserId == userId).ToListAsync();

        Assert.Equal(2, tokens.Count);
        Assert.Single(tokens.Where(t => t.RevokedAt is null));
        Assert.Contains(tokens, t => t.Token == refreshToken && t.RevokedAt is not null);
    }

    [Fact]
    public async Task LogoutAndRevokeAll_StillRevokeRefreshTokens()
    {
        var (userId, firstToken) = await LoginForTokensAsync();
        var (_, secondToken) = await LoginForTokensAsync();

        var logout = await client.PostAsJsonAsync("/auth/logout", new RevokeTokenRequest(firstToken));
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
            var firstRefreshToken = await db.RefreshTokens.SingleAsync(t => t.Token == firstToken);
            Assert.NotNull(firstRefreshToken.RevokedAt);

            var secondRefreshToken = await db.RefreshTokens.SingleAsync(t => t.Token == secondToken);
            Assert.Null(secondRefreshToken.RevokedAt);
        }

        using (var request = new HttpRequestMessage(HttpMethod.Post, "/auth/revoke-all"))
        {
            request.Headers.Add("X-Test-UserId", userId);

            var revokeAll = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, revokeAll.StatusCode);
        }

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var tokens = await verifyDb.RefreshTokens.Where(t => t.UserId == userId).ToListAsync();

        Assert.All(tokens, token => Assert.NotNull(token.RevokedAt));
    }

    [Fact]
    public async Task Refresh_WithUnknownToken_ReturnsStableGenericFailureContract()
    {
        const string unknownToken = "unknown-refresh-token";

        var response = await client.PostAsJsonAsync("/auth/refresh", new TokenRequest(unknownToken));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("refresh_invalid", json.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("message").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("correlationId").GetString()));
        Assert.DoesNotContain(unknownToken, json.RootElement.GetRawText());
    }

    [Fact]
    public async Task Logout_WithUnknownToken_IsIdempotentAndDoesNotDiscloseState()
    {
        const string unknownToken = "unknown-refresh-token";

        var response = await client.PostAsJsonAsync("/auth/logout", new RevokeTokenRequest(unknownToken));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    private async Task<(string UserId, string RefreshToken)> LoginForTokensAsync()
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("test", "test-passphrase-2026!"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var userId = json.RootElement.GetProperty("userId").GetString();
        var refreshToken = json.RootElement.GetProperty("refreshToken").GetString();

        Assert.False(string.IsNullOrWhiteSpace(userId));
        Assert.False(string.IsNullOrWhiteSpace(refreshToken));

        return (userId!, refreshToken!);
    }
}

public sealed class AuthRefreshRateLimitContractTests :
    IClassFixture<AuthRefreshRateLimitWebApplicationFactory>,
    IAsyncLifetime
{
    private readonly AuthRefreshRateLimitWebApplicationFactory factory;
    private readonly HttpClient client;

    public AuthRefreshRateLimitContractTests(AuthRefreshRateLimitWebApplicationFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        factory.RateLimitStore.Allow = true;
        using var scope = factory.Services.CreateScope();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var seeder = ActivatorUtilities.CreateInstance<TestAccountSeeder>(scope.ServiceProvider);
        await seeder.SeedAsync(environment);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Refresh_WhenRateLimited_ReturnsStableCodeAndRetryAfter()
    {
        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("test", "test-passphrase-2026!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var loginJson = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var refreshToken = loginJson.RootElement.GetProperty("refreshToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(refreshToken));

        factory.RateLimitStore.Allow = false;
        var response = await client.PostAsJsonAsync("/auth/refresh", new TokenRequest(refreshToken!));

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("17", response.Headers.GetValues("Retry-After").Single());
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("refresh_rate_limited", json.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("correlationId").GetString()));
    }
}

public sealed class AuthRefreshRateLimitWebApplicationFactory : CustomWebApplicationFactory<Program>
{
    public ToggleRateLimitStore RateLimitStore { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRateLimitCounterStore>();
            services.AddSingleton<IRateLimitCounterStore>(RateLimitStore);
        });
    }
}

public sealed class ToggleRateLimitStore : IRateLimitCounterStore
{
    public bool Allow { get; set; }

    public bool TryAcquire(
        string key,
        int limit,
        TimeSpan window,
        out int retryAfterSeconds,
        int maxPartitions = 100_000)
    {
        var rejectRefresh = !Allow && key.StartsWith("refresh:", StringComparison.Ordinal);
        retryAfterSeconds = rejectRefresh ? 17 : 0;
        return !rejectRefresh;
    }

    public RateLimitStoreSnapshot GetSnapshot() => new(
        PartitionCount: 0,
        AllowedRequests: 0,
        RejectedRequests: 0,
        SaturationRejections: 0,
        EvictedPartitions: 0,
        CleanupRuns: 0);
}
