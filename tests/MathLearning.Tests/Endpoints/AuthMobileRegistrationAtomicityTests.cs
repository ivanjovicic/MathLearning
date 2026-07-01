using System.Net;
using System.Net.Http.Json;
using MathLearning.Api;
using MathLearning.Application.DTOs.Auth;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System.Threading;

namespace MathLearning.Tests.Endpoints;

[Collection("AuthMobileRegistrationAtomicity")]
public sealed class AuthMobileRegistrationAtomicityTests :
    IClassFixture<AuthMobileRegistrationWebApplicationFactory>,
    IAsyncLifetime
{
    private readonly AuthMobileRegistrationWebApplicationFactory factory;
    private readonly HttpClient client;
    private readonly RegistrationFailureState failureState;

    public AuthMobileRegistrationAtomicityTests(AuthMobileRegistrationWebApplicationFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
        failureState = factory.FailureState;
    }

    public Task InitializeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
        TestDbContextFactory.SeedAsync(db).GetAwaiter().GetResult();
        failureState.Reset();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        failureState.Reset();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ProfileSaveFailure_CleansUpIdentityUserAndProfile()
    {
        var request = CreateRequest("profile");
        failureState.FailOnSaveCall = 2;

        var response = await client.PostAsJsonAsync("/auth/mobile/register", request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<MobileRegisterResponse>();
        Assert.NotNull(body);
        Assert.False(body!.Success);
        Assert.Equal("Registration failed. Please try again.", body.Message);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        Assert.Null(await userManager.FindByNameAsync(request.Username));
        Assert.False(await db.UserProfiles.AnyAsync(p => p.Username == request.Username));
        Assert.Equal(0, await db.RefreshTokens.CountAsync());
    }

    [Fact]
    public async Task RefreshTokenSaveFailure_CleansUpIdentityUserProfileAndToken()
    {
        var request = CreateRequest("refresh");
        failureState.FailOnSaveCall = 3;

        var response = await client.PostAsJsonAsync("/auth/mobile/register", request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<MobileRegisterResponse>();
        Assert.NotNull(body);
        Assert.False(body!.Success);
        Assert.Equal("Registration failed. Please try again.", body.Message);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        Assert.Null(await userManager.FindByNameAsync(request.Username));
        Assert.False(await db.UserProfiles.AnyAsync(p => p.Username == request.Username));
        Assert.Equal(0, await db.RefreshTokens.CountAsync());
    }

    [Fact]
    public async Task RetryAfterPartialFailure_DoesNotDoubleGrantWelcomeCoins()
    {
        var request = CreateRequest("retry");

        failureState.FailOnSaveCall = 2;
        var first = await client.PostAsJsonAsync("/auth/mobile/register", request);
        Assert.Equal(HttpStatusCode.InternalServerError, first.StatusCode);

        failureState.Reset();
        var second = await client.PostAsJsonAsync("/auth/mobile/register", request);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var body = await second.Content.ReadFromJsonAsync<MobileRegisterResponse>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.NotNull(body.Profile);
        Assert.NotNull(body.Tokens);
        Assert.Equal(request.Username, body.Profile!.Username);
        Assert.Equal(100, body.Profile.Coins);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        var user = await userManager.FindByNameAsync(request.Username);
        Assert.NotNull(user);

        var profile = await db.UserProfiles.SingleAsync(p => p.Username == request.Username);
        Assert.Equal(100, profile.Coins);
        Assert.Equal(1, await db.UserProfiles.CountAsync(p => p.Username == request.Username));
        Assert.Equal(1, await db.RefreshTokens.CountAsync(t => t.UserId == user!.Id));
    }

    private static MobileRegisterRequest CreateRequest(string suffix)
    {
        var unique = Guid.NewGuid().ToString("N");
        return new MobileRegisterRequest(
            Username: $"register-{suffix}-{unique}",
            Email: $"register-{suffix}-{unique}@mathlearning.local",
            Password: "Test123!",
            DisplayName: $"Register {suffix}",
            SchoolName: null,
            FacultyName: null);
    }
}

public sealed class AuthMobileRegistrationWebApplicationFactory : CustomWebApplicationFactory<Program>
{
    public RegistrationFailureState FailureState { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApiDbContext>>();
            services.RemoveAll<ApiDbContext>();

            var dbName = $"auth-mobile-register-{Guid.NewGuid():N}";
            var options = new DbContextOptionsBuilder<ApiDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;

            services.AddSingleton(options);
            services.AddSingleton(FailureState);
            services.AddScoped<ApiDbContext, RegistrationFailureApiDbContext>();
        });
    }
}

public sealed class RegistrationFailureState
{
    private int saveCallCount;

    public int? FailOnSaveCall { get; set; }

    public void Reset()
    {
        saveCallCount = 0;
        FailOnSaveCall = null;
    }

    public bool ShouldThrowOnCurrentSave()
    {
        var callNumber = Interlocked.Increment(ref saveCallCount);
        return FailOnSaveCall == callNumber;
    }
}

internal sealed class RegistrationFailureApiDbContext : ApiDbContext
{
    private readonly RegistrationFailureState state;

    public RegistrationFailureApiDbContext(
        DbContextOptions<ApiDbContext> options,
        RegistrationFailureState state)
        : base(options)
    {
        this.state = state;
    }

    public override int SaveChanges()
    {
        if (state.ShouldThrowOnCurrentSave())
            throw new InvalidOperationException(AuthMobileRegistrationAtomicityTestsSecret.SecretMessage);

        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (state.ShouldThrowOnCurrentSave())
            throw new InvalidOperationException(AuthMobileRegistrationAtomicityTestsSecret.SecretMessage);

        return base.SaveChangesAsync(cancellationToken);
    }
}

internal static class AuthMobileRegistrationAtomicityTestsSecret
{
    public const string SecretMessage = "SECRET_MOBILE_REGISTER_SAVE_FAILURE";
}

[CollectionDefinition("AuthMobileRegistrationAtomicity", DisableParallelization = true)]
public sealed class AuthMobileRegistrationAtomicityCollectionDefinition;
