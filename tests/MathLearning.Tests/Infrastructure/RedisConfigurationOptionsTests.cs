using MathLearning.Api.Startup;
using Microsoft.Extensions.Configuration;

namespace MathLearning.Tests.Infrastructure;

public sealed class RedisConfigurationOptionsTests
{
    [Fact]
    public void BuildRedisConfigurationOptions_UsesBoundedDefaults()
    {
        var config = BuildConfig();

        var options = ServiceRegistrationExtensions.BuildRedisConfigurationOptions(
            config,
            "localhost:6379");

        Assert.False(options.AbortOnConnectFail);
        Assert.Equal(2000, options.ConnectTimeout);
        Assert.Equal(2000, options.SyncTimeout);
        Assert.Equal(3, options.ConnectRetry);
        Assert.Equal(60, options.KeepAlive);
        Assert.Null(options.DefaultDatabase);
    }

    [Fact]
    public void BuildRedisConfigurationOptions_AppliesExplicitOverrides()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Redis:AbortOnConnectFail"] = "true",
            ["Redis:ConnectTimeoutMs"] = "750",
            ["Redis:SyncTimeoutMs"] = "1250",
            ["Redis:ConnectRetry"] = "5",
            ["Redis:KeepAliveSeconds"] = "30",
            ["Redis:DefaultDatabase"] = "7"
        });

        var options = ServiceRegistrationExtensions.BuildRedisConfigurationOptions(
            config,
            "localhost:6379");

        Assert.True(options.AbortOnConnectFail);
        Assert.Equal(750, options.ConnectTimeout);
        Assert.Equal(1250, options.SyncTimeout);
        Assert.Equal(5, options.ConnectRetry);
        Assert.Equal(30, options.KeepAlive);
        Assert.Equal(7, options.DefaultDatabase);
    }

    private static IConfiguration BuildConfig(Dictionary<string, string?>? values = null)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values ?? new Dictionary<string, string?>())
            .Build();
}
