using Hangfire;
using MathLearning.Api.Startup;
using MathLearning.Infrastructure.Services.EventBus;
using Microsoft.Extensions.Configuration;

namespace MathLearning.Tests.Services;

public sealed class HangfireOutboxIsolationTests
{
    [Fact]
    public void BackgroundWorkerOptions_ClampPoolAndDatabaseBudgets()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:CommandTimeoutSeconds"] = "7",
                ["Hangfire:WorkerCount"] = "99"
            })
            .Build();

        Assert.Equal(7, ServiceRegistrationExtensions.ResolveDatabaseCommandTimeoutSeconds(configuration));

        var serverOptions = ServiceRegistrationExtensions.BuildHangfireServerOptions(configuration);

        Assert.Equal(4, serverOptions.WorkerCount);
        Assert.Equal(new[] { "default" }, serverOptions.Queues);
        Assert.Equal(TimeSpan.FromSeconds(30), serverOptions.ShutdownTimeout);
    }

    [Fact]
    public void PreProductionIdle_DisablesPeriodicDatabaseWorkers_AndBacksOffOutbox()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BackgroundWork:Profile"] = "PreProductionIdle"
            })
            .Build();

        var backgroundWork = BackgroundWorkOptions.FromConfiguration(configuration);
        var outbox = new OutboxProcessingOptions
        {
            IdleDelay = backgroundWork.OutboxInitialIdleDelay,
            MaxIdleDelay = backgroundWork.OutboxMaxIdleDelay
        };

        Assert.False(backgroundWork.HangfireEnabled);
        Assert.False(backgroundWork.IndexMaintenanceEnabled);
        Assert.False(backgroundWork.XpResetEnabled);
        Assert.False(backgroundWork.WeaknessDailySweepEnabled);
        Assert.False(backgroundWork.ExplanationCacheCleanupEnabled);
        Assert.Equal(TimeSpan.FromMinutes(1), outbox.GetIdleDelay(0));
        Assert.Equal(TimeSpan.FromMinutes(10), outbox.GetIdleDelay(20));
    }

    [Fact]
    public void FullProfile_PreservesBackgroundWorkersAndLowLatencyOutbox()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BackgroundWork:Profile"] = "Full"
            })
            .Build();

        var backgroundWork = BackgroundWorkOptions.FromConfiguration(configuration);

        Assert.True(backgroundWork.HangfireEnabled);
        Assert.True(backgroundWork.IndexMaintenanceEnabled);
        Assert.True(backgroundWork.XpResetEnabled);
        Assert.True(backgroundWork.WeaknessDailySweepEnabled);
        Assert.True(backgroundWork.ExplanationCacheCleanupEnabled);
        Assert.Equal(TimeSpan.FromSeconds(1), backgroundWork.OutboxInitialIdleDelay);
        Assert.Equal(TimeSpan.FromSeconds(1), backgroundWork.OutboxMaxIdleDelay);
    }
}
