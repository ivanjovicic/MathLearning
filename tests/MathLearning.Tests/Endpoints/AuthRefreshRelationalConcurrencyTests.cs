using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MathLearning.Api;
using MathLearning.Api.Startup;
using MathLearning.Application.DTOs.Auth;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace MathLearning.Tests.Endpoints;

public sealed class AuthRefreshRelationalConcurrencyTests
{
    [Fact]
    public async Task ConcurrentRefreshRequests_RelationalConcurrency_AllowsExactlyOneRotation()
    {
        var factory = new RelationalAuthRefreshWebApplicationFactory();

        try
        {
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

            // Writer one is allowed through by the interceptor. Writer two remains blocked until
            // the first HTTP request has completed, guaranteeing its UPDATE sees the changed row.
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
        finally
        {
            factory.Dispose();
            factory.DeleteDatabaseFiles();
        }
    }
}

public sealed class RelationalAuthRefreshWebApplicationFactory : CustomWebApplicationFactory<Program>
{
    private readonly string databasePath =
        Path.Combine(Path.GetTempPath(), $"mathlearning-auth-refresh-{Guid.NewGuid():N}.db");
    private readonly string connectionString;

    public RelationalAuthRefreshWebApplicationFactory()
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

    public RefreshRotationSaveCoordinator RefreshCoordinator { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApiDbContext>>();
            services.RemoveAll<ApiDbContext>();

            var options = new DbContextOptionsBuilder<ApiDbContext>()
                .UseSqlite(connectionString)
                .AddInterceptors(RefreshCoordinator)
                .Options;

            services.AddSingleton(options);
            services.AddScoped<ApiDbContext>();
        });
    }

    public async Task SeedTestAccountAsync()
    {
        using var scope = Services.CreateScope();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var seeder = ActivatorUtilities.CreateInstance<TestAccountSeeder>(scope.ServiceProvider);
        await seeder.SeedAsync(environment);
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

public sealed class RefreshRotationSaveCoordinator : SaveChangesInterceptor
{
    private readonly ConcurrentDictionary<Guid, int> writerByContext = new();
    private readonly TaskCompletionSource<bool> bothWritersArrived =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> releaseSecondWriter =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int enabled;
    private int writerCount;

    public void Enable() => Interlocked.Exchange(ref enabled, 1);

    public async Task WaitUntilBothWritersArriveAsync() =>
        await bothWritersArrived.Task.WaitAsync(TimeSpan.FromSeconds(15));

    public void ReleaseSecondWriter() => releaseSecondWriter.TrySetResult(true);

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (Volatile.Read(ref enabled) == 0 || context is null || !IsRefreshRotation(context))
            return result;

        var contextId = context.ContextId.InstanceId;
        var writer = writerByContext.GetOrAdd(
            contextId,
            _ => Interlocked.Increment(ref writerCount));

        if (writer == 2)
            bothWritersArrived.TrySetResult(true);

        await bothWritersArrived.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);

        if (writer == 2)
            await releaseSecondWriter.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);

        return result;
    }

    private static bool IsRefreshRotation(DbContext context) =>
        context.ChangeTracker.Entries<RefreshToken>().Any(entry =>
            entry.State == EntityState.Modified &&
            entry.Property(x => x.RevokedAt).IsModified);
}
