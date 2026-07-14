using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using MathLearning.Core.DTOs;
using MathLearning.Core.Services;
using MathLearning.Infrastructure.Persistance;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace MathLearning.Tests.Helpers;

public class PostgresWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram>, IAsyncDisposable
    where TProgram : class
{
    private readonly PostgresTestDatabase database;
    private readonly string appDbName = $"pg-web-tests-app-{Guid.NewGuid():N}";

    public PostgresWebApplicationFactory(PostgresTestDatabase database)
    {
        this.database = database;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Test");
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Test");
        Environment.SetEnvironmentVariable("Database__StartupMode", "Skip");
        builder.UseEnvironment("Test");

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });

            services.RemoveAll<MathLearning.Application.Services.ILeaderboardService>();
            services.RemoveAll<MathLearning.Application.Services.ISchoolLeaderboardService>();
            services.RemoveAll<IRedisLeaderboardService>();
            services.RemoveAll<IBackgroundJobClient>();

            services.RemoveAll<DbContextOptions<ApiDbContext>>();
            services.RemoveAll<ApiDbContext>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            services.AddDbContext<ApiDbContext>(options =>
            {
                options.UseNpgsql(
                    database.DatabaseConnectionString,
                    npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
                ConfigureApiDb(options);
            });
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(appDbName));

            var removalKeywords = new[] { "IndexMaintenance", "XpReset", "WeaknessAnalysis", "WeaknessAnalysisScheduler", "WeaknessAnalysisDailyHostedService", "Hangfire", "Outbox" };
            var hosted = services.Where(sd => sd.ServiceType == typeof(IHostedService)).ToList();
            foreach (var sd in hosted)
            {
                var implTypeName = sd.ImplementationType?.Name;
                var instTypeName = sd.ImplementationInstance?.GetType().Name;
                var factoryMethodName = sd.ImplementationFactory?.Method.Name;
                var factoryDeclaringName = sd.ImplementationFactory?.Method.DeclaringType?.Name;

                var matches = removalKeywords.Any(keyword =>
                    (implTypeName != null && implTypeName.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ||
                    (instTypeName != null && instTypeName.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ||
                    (factoryMethodName != null && factoryMethodName.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ||
                    (factoryDeclaringName != null && factoryDeclaringName.Contains(keyword, StringComparison.OrdinalIgnoreCase)));

                if (matches)
                {
                    services.Remove(sd);
                }
            }

            services.AddSingleton<IBackgroundJobClient, FakeBackgroundJobClient>();
            services.AddSingleton<IRedisLeaderboardService, MockRedisLeaderboardService>();
            services.AddSingleton<MathLearning.Application.Services.ILeaderboardService, MockLeaderboardService>();
            services.AddSingleton<MathLearning.Application.Services.ISchoolLeaderboardService, MockLeaderboardService>();
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var apiDb = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        apiDb.Database.Migrate();
        TestDbContextFactory.SeedAsync(apiDb).GetAwaiter().GetResult();

        var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        appDb.Database.EnsureDeleted();
        appDb.Database.EnsureCreated();

        return host;
    }

    protected virtual void ConfigureApiDb(DbContextOptionsBuilder options)
    {
    }

    public new async ValueTask DisposeAsync()
    {
        Dispose();
        await database.DisposeAsync();
    }

    private sealed class FakeBackgroundJobClient : IBackgroundJobClient
    {
        public string Create(Job job, IState state) => Guid.NewGuid().ToString("N");

        public bool ChangeState(string jobId, IState state, string expectedState) => true;
    }
}
