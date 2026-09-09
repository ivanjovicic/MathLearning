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
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

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
        factory.RegistrationLogs.Messages.Clear();
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
        Assert.Contains(factory.RegistrationLogs.Messages,
            entry => entry.Level == LogLevel.Error && entry.Message.Contains("Reason=registration_unexpected"));
        Assert.DoesNotContain(factory.RegistrationLogs.Messages,
            entry => entry.Message.Contains(AuthMobileRegistrationAtomicityTestsSecret.SecretMessage)
                || entry.Message.Contains(request.Password) || entry.Message.Contains(request.Email));
        Assert.Equal("registration_unexpected", body.Code);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        Assert.Null(await userManager.FindByNameAsync(request.Username));
        Assert.False(await db.UserProfiles.AnyAsync(p => p.Username == request.Username));
        Assert.Equal(0, await db.RefreshTokens.CountAsync());
    }

    [Fact]
    public async Task ShortPassword_LogsSafeReasonWithoutAccountData()
    {
        var request = CreateRequest("validation") with { Password = "short1234" };
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/mobile/register")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.TryAddWithoutValidation("X-Correlation-ID", "ml-test-registration-1");

        var response = await client.SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("ml-test-registration-1", response.Headers.GetValues("X-Correlation-ID").Single());
        var body = await response.Content.ReadFromJsonAsync<MobileRegisterResponse>();
        Assert.False(body!.Success);
        Assert.Equal("Registration could not be completed", body.Message);
        Assert.Equal("invalid_password", body.Code);
        Assert.Contains(factory.RegistrationLogs.Messages,
            entry => entry.Level == LogLevel.Warning
                && entry.Message.Contains("Reason=password_length")
                && entry.Message.Contains("CorrelationId=ml-test-registration-1"));
        Assert.DoesNotContain(factory.RegistrationLogs.Messages,
            entry => entry.Message.Contains(request.Password) || entry.Message.Contains(request.Email)
                || entry.Message.Contains(request.Username));
    }

    [Fact]
    public async Task InvalidEmail_ReturnsSafeInvalidEmailCode()
    {
        var request = CreateRequest("email") with { Email = "not-an-email" };
        var response = await client.PostAsJsonAsync("/auth/mobile/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MobileRegisterResponse>();
        Assert.False(body!.Success);
        Assert.Equal("invalid_email", body.Code);
        Assert.Contains(factory.RegistrationLogs.Messages,
            entry => entry.Level == LogLevel.Warning && entry.Message.Contains("Reason=email_format"));
    }

    [Fact]
    public async Task Conflict_ReturnsGenericRegistrationConflictCode()
    {
        var request = CreateRequest("conflict");
        var first = await client.PostAsJsonAsync("/auth/mobile/register", request);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        factory.RegistrationLogs.Messages.Clear();
        var duplicate = await client.PostAsJsonAsync("/auth/mobile/register", request);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var body = await duplicate.Content.ReadFromJsonAsync<MobileRegisterResponse>();
        Assert.False(body!.Success);
        Assert.Equal("registration_conflict", body.Code);
        Assert.Equal("Registration could not be completed", body.Message);
        Assert.Contains(factory.RegistrationLogs.Messages,
            entry => entry.Level == LogLevel.Warning && entry.Message.Contains("Reason=account_conflict"));
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
        Assert.True(user!.EmailConfirmed);

        var profile = await db.UserProfiles.SingleAsync(p => p.Username == request.Username);
        Assert.Equal(100, profile.Coins);
        Assert.Equal(1, await db.UserProfiles.CountAsync(p => p.Username == request.Username));
        Assert.Equal(1, await db.RefreshTokens.CountAsync(t => t.UserId == user!.Id));

        var duplicate = await client.PostAsJsonAsync("/auth/mobile/register", request);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var duplicateBody = await duplicate.Content.ReadFromJsonAsync<MobileRegisterResponse>();
        Assert.NotNull(duplicateBody);
        Assert.False(duplicateBody!.Success);
        Assert.Equal("Registration could not be completed", duplicateBody.Message);
        Assert.Equal("registration_conflict", duplicateBody.Code);
    }

    private static MobileRegisterRequest CreateRequest(string suffix)
    {
        var unique = Guid.NewGuid().ToString("N");
        return new MobileRegisterRequest(
            Username: $"register-{suffix}-{unique}",
            Email: $"register-{suffix}-{unique}@mathlearning.local",
            Password: "MathLearningPassphrase2026!",
            DisplayName: $"Register {suffix}",
            SchoolName: null,
            FacultyName: null);
    }
}

public sealed class AuthMobileRegistrationWebApplicationFactory : CustomWebApplicationFactory<Program>
{
    public RegistrationFailureState FailureState { get; } = new();
    public RecordingRegistrationLogger RegistrationLogs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<ILogger<Program>>(RegistrationLogs);
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

public sealed class RecordingRegistrationLogger : ILogger<Program>
{
    public ConcurrentQueue<(LogLevel Level, string Message)> Messages { get; } = new();
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter) =>
        Messages.Enqueue((logLevel, formatter(state, exception)));
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
