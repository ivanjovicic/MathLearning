using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Application.DTOs.Auth;
using MathLearning.Application.Services;
using MathLearning.Core.Services;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Endpoints;

public sealed class AuthSessionInvalidationTests :
    IClassFixture<RealJwtWebApplicationFactory<Program>>
{
    private readonly RealJwtWebApplicationFactory<Program> factory;
    private readonly HttpClient client;

    public AuthSessionInvalidationTests(RealJwtWebApplicationFactory<Program> factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task RevokeAll_InvalidatesExistingBearerToken_AndFreshLoginStillWorks()
    {
        var firstSession = await RegisterMobileAsync("revoke-all");

        var beforeRevoke = await GetProfileAsync(firstSession.AccessToken);
        Assert.Equal(HttpStatusCode.OK, beforeRevoke.StatusCode);

        var revokeAll = await SendAuthenticatedAsync(HttpMethod.Post, "/auth/revoke-all", firstSession.AccessToken);
        Assert.Equal(HttpStatusCode.OK, revokeAll.StatusCode);

        var staleProfile = await GetProfileAsync(firstSession.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, staleProfile.StatusCode);

        var secondLogin = await LoginAsync(firstSession.Username, firstSession.Password);
        var afterRevoke = await GetProfileAsync(secondLogin.AccessToken);
        Assert.Equal(HttpStatusCode.OK, afterRevoke.StatusCode);
    }

    [Fact]
    public async Task RoleRemoval_RejectsBearerAndRefreshTokens()
    {
        var (username, password) = await CreateAdminUserAsync();
        var login = await LoginAsync(username, password);

        var adminRequest = await SendAuthenticatedAsync(HttpMethod.Get, "/api/monitoring/logs", login.AccessToken);
        Assert.Equal(HttpStatusCode.OK, adminRequest.StatusCode);

        await RemoveAdminRoleAsync(username);

        var staleAdminRequest = await SendAuthenticatedAsync(HttpMethod.Get, "/api/monitoring/logs", login.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, staleAdminRequest.StatusCode);

        var refresh = await client.PostAsJsonAsync("/auth/refresh", new TokenRequest(login.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task LockedAccount_RejectsBearerToken()
    {
        var session = await RegisterMobileAsync("lockout");

        await LockUserAsync(session.Username);

        var lockedProfile = await GetProfileAsync(session.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, lockedProfile.StatusCode);
    }

    [Fact]
    public async Task DeletedAccount_RejectsBearerToken()
    {
        var session = await RegisterMobileAsync("delete");

        await DeleteUserAsync(session.Username);

        var deletedProfile = await GetProfileAsync(session.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, deletedProfile.StatusCode);
    }

    private async Task<HttpResponseMessage> GetProfileAsync(string accessToken)
        => await SendAuthenticatedAsync(HttpMethod.Get, "/api/users/profile", accessToken);

    private async Task<HttpResponseMessage> SendAuthenticatedAsync(
        HttpMethod method,
        string path,
        string accessToken,
        HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = content;
        return await client.SendAsync(request);
    }

    private async Task<LoginResult> LoginAsync(string username, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return new LoginResult(
            json.RootElement.GetProperty("accessToken").GetString()!,
            json.RootElement.GetProperty("refreshToken").GetString()!,
            json.RootElement.GetProperty("userId").GetString()!,
            json.RootElement.GetProperty("username").GetString()!);
    }

    private async Task<MobileSession> RegisterMobileAsync(string tag)
    {
        var username = $"{tag}-{Guid.NewGuid():N}";
        var password = $"{tag}-passphrase-2026!";
        var email = $"{username}@mathlearning.local";

        var response = await client.PostAsJsonAsync(
            "/auth/mobile/register",
            new MobileRegisterRequest(username, email, password, DisplayName: username));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return new MobileSession(
            username,
            password,
            json.RootElement.GetProperty("tokens").GetProperty("accessToken").GetString()!,
            json.RootElement.GetProperty("tokens").GetProperty("refreshToken").GetString()!);
    }

    private async Task<(string Username, string Password)> CreateAdminUserAsync()
    {
        var username = $"admin-{Guid.NewGuid():N}";
        var password = "admin-passphrase-2026!";

        using var scope = factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        if (!await roleManager.RoleExistsAsync(DesignTokenSecurity.AdminRole))
        {
            var roleResult = await roleManager.CreateAsync(new IdentityRole(DesignTokenSecurity.AdminRole));
            Assert.True(roleResult.Succeeded);
        }

        var user = new IdentityUser
        {
            UserName = username,
            Email = $"{username}@mathlearning.local",
            EmailConfirmed = true,
            LockoutEnabled = true
        };

        var createResult = await userManager.CreateAsync(user, password);
        Assert.True(createResult.Succeeded);

        var roleAddResult = await userManager.AddToRoleAsync(user, DesignTokenSecurity.AdminRole);
        Assert.True(roleAddResult.Succeeded);

        var stampResult = await userManager.UpdateSecurityStampAsync(user);
        Assert.True(stampResult.Succeeded);

        return (username, password);
    }

    private async Task RemoveAdminRoleAsync(string username)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await userManager.FindByNameAsync(username);
        Assert.NotNull(user);

        var roleResult = await userManager.RemoveFromRoleAsync(user!, DesignTokenSecurity.AdminRole);
        Assert.True(roleResult.Succeeded);

        var stampResult = await userManager.UpdateSecurityStampAsync(user!);
        Assert.True(stampResult.Succeeded);
    }

    private async Task LockUserAsync(string username)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await userManager.FindByNameAsync(username);
        Assert.NotNull(user);

        var lockoutEnd = DateTimeOffset.UtcNow.AddMinutes(30);
        var lockoutResult = await userManager.SetLockoutEndDateAsync(user!, lockoutEnd);
        Assert.True(lockoutResult.Succeeded);
    }

    private async Task DeleteUserAsync(string username)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await userManager.FindByNameAsync(username);
        Assert.NotNull(user);

        var deleteResult = await userManager.DeleteAsync(user!);
        Assert.True(deleteResult.Succeeded);
    }

    private sealed record LoginResult(string AccessToken, string RefreshToken, string UserId, string Username);
    private sealed record MobileSession(string Username, string Password, string AccessToken, string RefreshToken);
}

public sealed class RealJwtWebApplicationFactory<TProgram> : CustomWebApplicationFactory<TProgram>
    where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            });
        });
    }
}
