using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Api.Services;
using MathLearning.Application.DTOs.Auth;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Endpoints;

public sealed class AuthIncompleteAccountTests : IClassFixture<CustomWebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory<Program> factory;
    private readonly HttpClient client;

    public AuthIncompleteAccountTests(CustomWebApplicationFactory<Program> factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    public Task InitializeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
        TestDbContextFactory.SeedAsync(db).GetAwaiter().GetResult();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task LegacyRegister_CreatesIdentityProfileAndTokens()
    {
        var unique = Guid.NewGuid().ToString("N");
        var request = new RegisterRequest(
            Username: $"legacy-{unique}",
            Email: $"legacy-{unique}@mathlearning.local",
            Password: "MathLearningPassphrase2026!");

        var response = await client.PostAsJsonAsync("/auth/register", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(tokens);
        Assert.False(string.IsNullOrWhiteSpace(tokens!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));
        Assert.Equal(request.Username, tokens.Username);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        var user = await userManager.FindByNameAsync(request.Username);
        Assert.NotNull(user);
        var profile = await db.UserProfiles.SingleAsync(p => p.UserId == user!.Id);
        Assert.Equal(request.Username, profile.Username);
        Assert.Equal(100, profile.Coins);
        Assert.Equal(1, await db.RefreshTokens.CountAsync(t => t.UserId == user.Id));
    }

    [Fact]
    public async Task Login_DeniesIdentityOnlyAccountWithoutIssuingTokens()
    {
        var unique = Guid.NewGuid().ToString("N");
        var username = $"orphan-{unique}";
        var password = "MathLearningPassphrase2026!";

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var create = await userManager.CreateAsync(
                new IdentityUser
                {
                    UserName = username,
                    Email = $"orphan-{unique}@mathlearning.local",
                    EmailConfirmed = true,
                    LockoutEnabled = true
                },
                password);
            Assert.True(create.Succeeded);
        }

        var response = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest(Username: username, Password: password));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Account setup incomplete", doc.RootElement.GetProperty("error").GetString());

        using var assertScope = factory.Services.CreateScope();
        var db = assertScope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var userManagerAssert = assertScope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await userManagerAssert.FindByNameAsync(username);
        Assert.NotNull(user);
        Assert.False(await db.UserProfiles.AnyAsync(p => p.UserId == user!.Id));
        Assert.Equal(0, await db.RefreshTokens.CountAsync(t => t.UserId == user.Id));
    }

    [Fact]
    public async Task IncompleteAccountScan_DetectsIdentityOnlyUsers()
    {
        var unique = Guid.NewGuid().ToString("N");
        string orphanId;

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = new IdentityUser
            {
                UserName = $"scan-{unique}",
                Email = $"scan-{unique}@mathlearning.local",
                EmailConfirmed = true
            };
            Assert.True((await userManager.CreateAsync(user, "MathLearningPassphrase2026!")).Succeeded);
            orphanId = user.Id;
        }

        using var scanScope = factory.Services.CreateScope();
        var provisioning = scanScope.ServiceProvider.GetRequiredService<IAccountProvisioningService>();
        var scan = await provisioning.ScanIncompleteAccountsAsync();

        Assert.True(scan.IdentityOnlyCount >= 1);
        Assert.Contains(orphanId, scan.IdentityOnlyUserIds);
    }

    [Fact]
    public async Task MobileRegister_StillCreatesCompleteAccountViaProvisioningOwner()
    {
        var unique = Guid.NewGuid().ToString("N");
        var request = new MobileRegisterRequest(
            Username: $"mobile-{unique}",
            Email: $"mobile-{unique}@mathlearning.local",
            Password: "MathLearningPassphrase2026!",
            DisplayName: "Mobile User",
            SchoolName: null,
            FacultyName: null);

        var response = await client.PostAsJsonAsync("/auth/mobile/register", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<MobileRegisterResponse>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.NotNull(body.Tokens);
        Assert.NotNull(body.Profile);
        Assert.Equal(100, body.Profile!.Coins);
    }
}
