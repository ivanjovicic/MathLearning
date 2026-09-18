using System.Net;
using System.Net.Http.Json;
using System.Collections.Concurrent;
using MathLearning.Api;
using MathLearning.Api.Services;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace MathLearning.Tests.Endpoints;

public sealed class PasswordResetDeliveryOptionsTests
{
    private readonly PasswordResetDeliveryOptionsValidator validator = new();

    [Fact]
    public void DisabledDelivery_AllowsMissingProviderConfiguration()
    {
        var result = validator.Validate(Options.DefaultName, new PasswordResetDeliveryOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void EnabledDelivery_RejectsIncompleteProviderConfiguration()
    {
        var result = validator.Validate(Options.DefaultName, new PasswordResetDeliveryOptions
        {
            Enabled = true,
            SmtpHost = "smtp.example.test",
            FromAddress = "reset@example.test",
            ResetBaseUrl = "mathlearning://reset-password",
            SmtpUsername = "mailer"
        });

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("SmtpUsername and SmtpPassword", StringComparison.Ordinal));
    }

    [Fact]
    public void EnabledDelivery_AcceptsValidDeepLinkAndSmtpConfiguration()
    {
        var result = validator.Validate(Options.DefaultName, new PasswordResetDeliveryOptions
        {
            Enabled = true,
            SmtpHost = "smtp.example.test",
            SmtpPort = 587,
            FromAddress = "reset@example.test",
            ResetBaseUrl = "mathlearning://reset-password",
            SmtpUsername = "mailer",
            SmtpPassword = "secret-from-environment"
        });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void EnabledDelivery_RejectsInvalidPortAddressAndResetBaseUrl()
    {
        var result = validator.Validate(Options.DefaultName, new PasswordResetDeliveryOptions
        {
            Enabled = true,
            SmtpHost = "smtp.example.test",
            SmtpPort = 70000,
            FromAddress = "MathLearning <not-an-address>",
            ResetBaseUrl = "not a uri"
        });

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("SmtpPort", StringComparison.Ordinal));
        Assert.Contains(result.Failures!, failure => failure.Contains("FromAddress", StringComparison.Ordinal));
        Assert.Contains(result.Failures!, failure => failure.Contains("ResetBaseUrl", StringComparison.Ordinal));
    }
}

public sealed class AuthPasswordResetAtomicityTests
{
    [Theory]
    [InlineData(PasswordResetFailureTarget.PasswordMutation)]
    [InlineData(PasswordResetFailureTarget.RefreshTokenCleanup)]
    public async Task RelationalFailure_RollsBackPasswordStampAndRefreshTokens(
        PasswordResetFailureTarget target)
    {
        var factory = new PasswordResetRelationalWebApplicationFactory();
        try
        {
            using (var client = factory.CreateClient())
            {
                var account = await CreateAccountAsync(factory);
                var session = await LoginAsync(client, account.Username, account.OldPassword);
                var resetToken = await RequestResetTokenAsync(factory, client, account.Email);

                factory.FailureInterceptor.Arm(target);
                var reset = await client.PostAsJsonAsync(
                    "/auth/password/reset",
                    new
                    {
                        email = account.Email,
                        token = resetToken,
                        newPassword = account.NewPassword
                    });

                Assert.Equal(HttpStatusCode.ServiceUnavailable, reset.StatusCode);
                Assert.Equal(1, factory.FailureInterceptor.ThrowCount);

                var oldLogin = await client.PostAsJsonAsync(
                    "/auth/login",
                    new { username = account.Username, password = account.OldPassword });
                Assert.Equal(HttpStatusCode.OK, oldLogin.StatusCode);

                var newLogin = await client.PostAsJsonAsync(
                    "/auth/login",
                    new { username = account.Username, password = account.NewPassword });
                Assert.Equal(HttpStatusCode.Unauthorized, newLogin.StatusCode);

                using (var scope = factory.Services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<MathLearning.Infrastructure.Persistance.ApiDbContext>();
                    var originalRefresh = await db.RefreshTokens.SingleAsync(token => token.Token == session.RefreshToken);
                    Assert.Null(originalRefresh.RevokedAt);
                }
            }
        }
        finally
        {
            factory.Dispose();
            factory.DeleteDatabaseFiles();
        }
    }

    [Fact]
    public async Task InvalidToken_FailsBeforeMutationAndPreservesOldPassword()
    {
        var factory = new PasswordResetRelationalWebApplicationFactory();
        try
        {
            using (var client = factory.CreateClient())
            {
                var account = await CreateAccountAsync(factory);

                var reset = await client.PostAsJsonAsync(
                    "/auth/password/reset",
                    new
                    {
                        email = account.Email,
                        token = "invalid-reset-token",
                        newPassword = account.NewPassword
                    });

                Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
                var oldLogin = await client.PostAsJsonAsync(
                    "/auth/login",
                    new { username = account.Username, password = account.OldPassword });
                Assert.Equal(HttpStatusCode.OK, oldLogin.StatusCode);
                var newLogin = await client.PostAsJsonAsync(
                    "/auth/login",
                    new { username = account.Username, password = account.NewPassword });
                Assert.Equal(HttpStatusCode.Unauthorized, newLogin.StatusCode);
            }
        }
        finally
        {
            factory.Dispose();
            factory.DeleteDatabaseFiles();
        }
    }

    private static async Task<(string Username, string Email, string OldPassword, string NewPassword)> CreateAccountAsync(
        PasswordResetRelationalWebApplicationFactory factory)
    {
        var username = $"atomic-{Guid.NewGuid():N}";
        var account = (username, $"{username}@mathlearning.local", "OldMathLearningPassphrase2026!", "NewMathLearningPassphrase2026!");
        using var scope = factory.Services.CreateScope();
        var provisioning = scope.ServiceProvider.GetRequiredService<IAccountProvisioningService>();
        var result = await provisioning.CreateCompleteAccountAsync(account.Item1, account.Item2, account.Item3, account.Item1);
        Assert.True(result.Succeeded);
        return (account.Item1, account.Item2, account.Item3, account.Item4);
    }

    private static async Task<(string AccessToken, string RefreshToken)> LoginAsync(
        HttpClient client,
        string username,
        string password)
    {
        var response = await client.PostAsJsonAsync("/auth/login", new { username, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        return (json.GetProperty("accessToken").GetString()!, json.GetProperty("refreshToken").GetString()!);
    }

    private static async Task<string> RequestResetTokenAsync(
        PasswordResetRelationalWebApplicationFactory factory,
        HttpClient client,
        string email)
    {
        var response = await client.PostAsJsonAsync("/auth/password/forgot", new { email });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var delivery = Assert.IsType<InMemoryPasswordResetDelivery>(
            scope.ServiceProvider.GetRequiredService<IPasswordResetDelivery>());
        return Assert.Single(delivery.Messages.Where(message => message.RecipientEmail == email)).ResetToken;
    }
}

public enum PasswordResetFailureTarget
{
    PasswordMutation,
    RefreshTokenCleanup
}

public sealed class PasswordResetRelationalWebApplicationFactory : CustomWebApplicationFactory<Program>
{
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"mathlearning-password-reset-{Guid.NewGuid():N}.db");
    private readonly string connectionString;

    public PasswordResetRelationalWebApplicationFactory()
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

    public PasswordResetFailureInterceptor FailureInterceptor { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<MathLearning.Infrastructure.Persistance.ApiDbContext>>();
            services.RemoveAll<MathLearning.Infrastructure.Persistance.ApiDbContext>();

            var options = new DbContextOptionsBuilder<MathLearning.Infrastructure.Persistance.ApiDbContext>()
                .UseSqlite(connectionString)
                .AddInterceptors(FailureInterceptor)
                .Options;
            services.AddSingleton(options);
            services.AddScoped<MathLearning.Infrastructure.Persistance.ApiDbContext>();
        });
    }

    public void DeleteDatabaseFiles()
    {
        foreach (var path in new[] { databasePath, $"{databasePath}-wal", $"{databasePath}-shm" })
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}

public sealed class PasswordResetFailureInterceptor : SaveChangesInterceptor
{
    private readonly ConcurrentDictionary<Guid, PasswordResetFailureTarget> matchingSaves = new();
    private PasswordResetFailureTarget target;
    private int armed;
    private int throwCount;

    public int ThrowCount => Volatile.Read(ref throwCount);

    public void Arm(PasswordResetFailureTarget failureTarget)
    {
        target = failureTarget;
        matchingSaves.Clear();
        Interlocked.Exchange(ref throwCount, 0);
        Interlocked.Exchange(ref armed, 1);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (Volatile.Read(ref armed) == 1 && context is not null && IsTargetSave(context))
            matchingSaves[context.ContextId.InstanceId] = target;
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
            throw new InvalidOperationException("Password reset transactional failure.");
        }
        return ValueTask.FromResult(result);
    }

    private bool IsTargetSave(DbContext context) => target switch
    {
        PasswordResetFailureTarget.PasswordMutation => context.ChangeTracker.Entries<IdentityUser>()
            .Any(entry => entry.State == EntityState.Modified && entry.Property(user => user.PasswordHash).IsModified),
        PasswordResetFailureTarget.RefreshTokenCleanup => context.ChangeTracker.Entries<MathLearning.Domain.Entities.RefreshToken>()
            .Any(entry => entry.State == EntityState.Modified && entry.Property(token => token.RevokedAt).IsModified),
        _ => false
    };
}
