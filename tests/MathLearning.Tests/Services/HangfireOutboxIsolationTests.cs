using Hangfire;
using MathLearning.Api.Startup;
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
}
