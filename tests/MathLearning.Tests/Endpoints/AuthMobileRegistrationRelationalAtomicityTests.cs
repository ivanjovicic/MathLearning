using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using MathLearning.Api;
using MathLearning.Application.DTOs.Auth;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MathLearning.Tests.Endpoints;

public sealed class AuthMobileRegistrationRelationalAtomicityTests
{
    [Theory]
    [InlineData(RegistrationFailureTarget.Profile)]
    [InlineData(RegistrationFailureTarget.RefreshToken)]
    public async Task FailureAfterRelationalSave_RollsBackIdentityProfileAndRefreshToken(
        RegistrationFailureTarget target)
    {
        var factory = new RelationalMobileRegistrationWebApplicationFactory();

        try
        {
            using var client = factory.CreateClient();
            var request = CreateRequest(target.ToString().ToLowerInvariant());
            factory.FailureInterceptor.Arm(target);

            var response = await client.PostAsJsonAsync("/auth/mobile/register", request);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            var responseText = await response.Content.ReadAsStringAsync();
            Assert.Contains("Registration failed. Please try again.", responseText, StringComparison.Ordinal);
            Assert.DoesNotContain(RelationalRegistrationFailureInterceptor.SecretMessage, responseText, StringComparison.Ordinal);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

            Assert.Null(await userManager.FindByNameAsync(request.Username));
            Assert.False(await db.UserProfiles.AnyAsync(x => x.Username == request.Username));
            Assert.False(await db.RefreshTokens.AnyAsync());
            Assert.Equal(1, factory.FailureInterceptor.ThrowCount);
        }
        finally
        {
            factory.Dispose();
            factory.DeleteDatabaseFiles();
        }
    }

    [Fact]
    public async Task RetryAfterRelationalTokenFailure_CreatesExactlyOneAccountAndWelcomeGrant()
    {
        var factory = new RelationalMobileRegistrationWebApplicationFactory();

        try
        {
            using var client = factory.CreateClient();
            var request = CreateRequest("retry");
            factory.FailureInterceptor.Arm(RegistrationFailureTarget.RefreshToken);

            var failed = await client.PostAsJsonAsync("/auth/mobile/register", request);
            Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);

            factory.FailureInterceptor.Disarm();
            var retry = await client.PostAsJsonAsync("/auth/mobile/register", request);

            Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
            var body = await retry.Content.ReadFromJsonAsync<MobileRegisterResponse>();
            Assert.NotNull(body);
            Assert.True(body!.Success);
            Assert.NotNull(body.Profile);
            Assert.NotNull(body.Tokens);
            Assert.Equal(100, body.Profile!.Coins);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = await userManager.FindByNameAsync(request.Username);

            Assert.NotNull(user);
            Assert.True(user!.EmailConfirmed);
            Assert.Equal(1, await db.Users.CountAsync(x => x.UserName == request.Username));
            Assert.Equal(1, await db.UserProfiles.CountAsync(x => x.Username == request.Username));

            var profile = await db.UserProfiles.SingleAsync(x => x.Username == request.Username);
            Assert.Equal(100, profile.Coins);
            Assert.Equal(1, await db.RefreshTokens.CountAsync(x => x.UserId == user!.Id));
        }
        finally
        {
            factory.Dispose();
            factory.DeleteDatabaseFiles();
        }
    }

    private static MobileRegisterRequest CreateRequest(string suffix)
    {
        var unique = Guid.NewGuid().ToString("N");
        return new MobileRegisterRequest(
            Username: $"rel-register-{suffix}-{unique}",
            Email: $"rel-register-{suffix}-{unique}@mathlearning.local",
            Password: "MathLearningPassphrase2026!",
            DisplayName: $"Relational Register {suffix}",
            SchoolName: null,
            FacultyName: null);
    }
}

public enum RegistrationFailureTarget
{
    Profile,
    RefreshToken
}

public sealed class RelationalMobileRegistrationWebApplicationFactory : CustomWebApplicationFactory<Program>
{
    private readonly string databasePath =
        Path.Combine(Path.GetTempPath(), $"mathlearning-registration-{Guid.NewGuid():N}.db");
    private readonly string connectionString;

    public RelationalMobileRegistrationWebApplicationFactory()
    {
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false,
            DefaultTimeout = 30
        }.ToString();
    }

    public RelationalRegistrationFailureInterceptor FailureInterceptor { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApiDbContext>>();
            services.RemoveAll<ApiDbContext>();

            var options = new DbContextOptionsBuilder<ApiDbContext>()
                .UseSqlite(connectionString)
                .AddInterceptors(FailureInterceptor)
                .Options;

            services.AddSingleton(options);
            services.AddScoped<ApiDbContext>();
        });
    }

    public void DeleteDatabaseFiles()
    {
        DeleteIfExists(databasePath);
        DeleteIfExists($"{databasePath}-wal");
        DeleteIfExists($"{databasePath}-shm");
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}

public sealed class RelationalRegistrationFailureInterceptor : SaveChangesInterceptor
{
    public const string SecretMessage = "SECRET_RELATIONAL_REGISTRATION_AFTER_SAVE_FAILURE";

    private readonly ConcurrentDictionary<Guid, bool> matchingSaves = new();
    private RegistrationFailureTarget target;
    private int armed;
    private int throwCount;

    public int ThrowCount => Volatile.Read(ref throwCount);

    public void Arm(RegistrationFailureTarget failureTarget)
    {
        target = failureTarget;
        matchingSaves.Clear();
        Interlocked.Exchange(ref throwCount, 0);
        Interlocked.Exchange(ref armed, 1);
    }

    public void Disarm()
    {
        matchingSaves.Clear();
        Interlocked.Exchange(ref armed, 0);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (Volatile.Read(ref armed) == 1 && context is not null && IsTargetSave(context))
            matchingSaves[context.ContextId.InstanceId] = true;

        return ValueTask.FromResult(result);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context is not null &&
            matchingSaves.TryRemove(context.ContextId.InstanceId, out _) &&
            Interlocked.CompareExchange(ref throwCount, 1, 0) == 0)
        {
            throw new InvalidOperationException(SecretMessage);
        }

        return ValueTask.FromResult(result);
    }

    private bool IsTargetSave(DbContext context) => target switch
    {
        RegistrationFailureTarget.Profile =>
            context.ChangeTracker.Entries<UserProfile>().Any(x => x.State == EntityState.Added),
        RegistrationFailureTarget.RefreshToken =>
            context.ChangeTracker.Entries<RefreshToken>().Any(x => x.State == EntityState.Added),
        _ => false
    };
}
