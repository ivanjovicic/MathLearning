using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Api.Startup;
using MathLearning.Application.DTOs.Auth;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace MathLearning.Tests.Endpoints;

public sealed class AuthSafeErrorResponseTests :
    IClassFixture<AuthSafeErrorWebApplicationFactory>,
    IAsyncLifetime
{
    internal const string SecretMessage = "SECRET_REFRESH_TOKEN_SAVE_FAILURE";

    private readonly AuthSafeErrorWebApplicationFactory factory;
    private readonly HttpClient client;

    public AuthSafeErrorResponseTests(AuthSafeErrorWebApplicationFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        ThrowOnSaveApiDbContext.ThrowOnSave = false;

        using var scope = factory.Services.CreateScope();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var seeder = ActivatorUtilities.CreateInstance<TestAccountSeeder>(scope.ServiceProvider);
        await seeder.SeedAsync(environment);
    }

    public Task DisposeAsync()
    {
        ThrowOnSaveApiDbContext.ThrowOnSave = false;
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Login_WhenUnexpectedFailure_ReturnsSafeErrorWithoutRawMessage()
    {
        ThrowOnSaveApiDbContext.ThrowOnSave = true;
        try
        {
            var response = await client.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest("test", "test123"));

            await AssertSafeErrorResponseAsync(response);
        }
        finally
        {
            ThrowOnSaveApiDbContext.ThrowOnSave = false;
        }
    }

    [Fact]
    public async Task Refresh_WhenUnexpectedFailure_ReturnsSafeErrorWithoutRawMessage()
    {
        var (_, refreshToken) = await LoginForTokensAsync();

        ThrowOnSaveApiDbContext.ThrowOnSave = true;
        try
        {
            var response = await client.PostAsJsonAsync(
                "/auth/refresh",
                new TokenRequest(refreshToken));

            await AssertSafeErrorResponseAsync(response);
        }
        finally
        {
            ThrowOnSaveApiDbContext.ThrowOnSave = false;
        }
    }

    [Fact]
    public async Task Logout_WhenUnexpectedFailure_ReturnsSafeErrorWithoutRawMessage()
    {
        var (_, refreshToken) = await LoginForTokensAsync();

        ThrowOnSaveApiDbContext.ThrowOnSave = true;
        try
        {
            var response = await client.PostAsJsonAsync(
                "/auth/logout",
                new RevokeTokenRequest(refreshToken));

            await AssertSafeErrorResponseAsync(response);
        }
        finally
        {
            ThrowOnSaveApiDbContext.ThrowOnSave = false;
        }
    }

    [Fact]
    public async Task RevokeAll_WhenUnexpectedFailure_ReturnsSafeErrorWithoutRawMessage()
    {
        var (userId, _) = await LoginForTokensAsync();

        ThrowOnSaveApiDbContext.ThrowOnSave = true;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/revoke-all");
            request.Headers.Add("X-Test-UserId", userId);

            var response = await client.SendAsync(request);
            await AssertSafeErrorResponseAsync(response);
        }
        finally
        {
            ThrowOnSaveApiDbContext.ThrowOnSave = false;
        }
    }

    private async Task<(string UserId, string RefreshToken)> LoginForTokensAsync()
    {
        ThrowOnSaveApiDbContext.ThrowOnSave = false;

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("test", "test123"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var userId = json.RootElement.GetProperty("userId").GetString();
        var refreshToken = json.RootElement.GetProperty("refreshToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(userId));
        Assert.False(string.IsNullOrWhiteSpace(refreshToken));
        return (userId!, refreshToken!);
    }

    private static async Task AssertSafeErrorResponseAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(SecretMessage, body);

        using var json = JsonDocument.Parse(body);
        Assert.Equal("An unexpected error occurred.", json.RootElement.GetProperty("error").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("traceId").GetString()));
    }
}

public sealed class AuthSafeErrorWebApplicationFactory : CustomWebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApiDbContext>>();
            services.RemoveAll<ApiDbContext>();

            var dbName = $"auth-safe-errors-{Guid.NewGuid():N}";
            var options = new DbContextOptionsBuilder<ApiDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;

            services.AddSingleton(options);
            services.AddScoped<ApiDbContext, ThrowOnSaveApiDbContext>();
        });
    }
}

internal sealed class ThrowOnSaveApiDbContext : ApiDbContext
{
    public static bool ThrowOnSave { get; set; }

    public ThrowOnSaveApiDbContext(DbContextOptions<ApiDbContext> options)
        : base(options)
    {
    }

    public override int SaveChanges()
    {
        if (ThrowOnSave)
            throw new InvalidOperationException(AuthSafeErrorResponseTests.SecretMessage);

        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (ThrowOnSave)
            throw new InvalidOperationException(AuthSafeErrorResponseTests.SecretMessage);

        return base.SaveChangesAsync(cancellationToken);
    }
}
