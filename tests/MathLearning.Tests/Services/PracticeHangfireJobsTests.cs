using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using MathLearning.Api.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace MathLearning.Tests.Services;

public sealed class PracticeHangfireJobsTests
{
    [Fact]
    public async Task DailyAggregationJob_RerunSameDay_EnqueuesSameActiveUserWorkload()
    {
        var dbName = $"practice-daily-{Guid.NewGuid():N}";
        var countingClient = new CountingBackgroundJobClient();

        await using (var setup = TestDbContextFactory.Create(dbName))
        {
            setup.UserProfiles.Add(new UserProfile
            {
                UserId = "active-user",
                Username = "active",
                DisplayName = "Active",
                LastActivityDay = DateOnly.FromDateTime(DateTime.UtcNow),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            setup.UserProfiles.Add(new UserProfile
            {
                UserId = "inactive-user",
                Username = "inactive",
                DisplayName = "Inactive",
                LastActivityDay = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-60),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await setup.SaveChangesAsync();
        }

        var services = new ServiceCollection();
        services.AddDbContext<ApiDbContext>(options => options.UseInMemoryDatabase(dbName));
        services.AddSingleton<IBackgroundJobClient>(countingClient);
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();

        var jobs = new PracticeHangfireJobs(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PracticeHangfireJobs>.Instance);

        await jobs.DailyAggregationJob();
        var firstRunEnqueued = countingClient.EnqueuedCount;

        await jobs.DailyAggregationJob();
        var secondRunEnqueued = countingClient.EnqueuedCount;

        Assert.Equal(3, firstRunEnqueued);
        Assert.Equal(6, secondRunEnqueued);
    }

    private sealed class CountingBackgroundJobClient : IBackgroundJobClient
    {
        public int EnqueuedCount { get; private set; }

        public string Create(Job job, IState state)
        {
            EnqueuedCount++;
            return Guid.NewGuid().ToString("N");
        }

        public bool ChangeState(string jobId, IState state, string expectedState) => true;
    }
}
