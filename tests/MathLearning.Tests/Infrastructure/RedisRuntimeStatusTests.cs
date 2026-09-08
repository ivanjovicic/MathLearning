using MathLearning.Api.Startup;
using Microsoft.Extensions.Configuration;

namespace MathLearning.Tests.Infrastructure;

public sealed class RedisRuntimeStatusTests
{
    [Fact]
    public void MissingRedisConfigurationDefaultsToOptionalDbFallback()
    {
        var configuration = new ConfigurationBuilder().Build();

        var resolved = ServiceRegistrationExtensions.ResolveRedisConfiguration(configuration);
        var status = new RedisRuntimeStatus(resolved.Required);
        status.MarkDbFallback("ConnectionStringMissing");

        var snapshot = status.Snapshot();
        Assert.False(snapshot.Required);
        Assert.False(snapshot.Configured);
        Assert.False(snapshot.Connected);
        Assert.Equal("DbFallback", snapshot.Mode);
        Assert.Equal("ConnectionStringMissing", snapshot.FailureReason);
    }

    [Fact]
    public void RequiredRedisConfigurationIsVisibleAndNotSilentlyOptional()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:Required"] = "true"
            })
            .Build();

        var resolved = ServiceRegistrationExtensions.ResolveRedisConfiguration(configuration);
        var status = new RedisRuntimeStatus(resolved.Required);
        status.MarkDbFallback("ConnectionStringMissing");

        var snapshot = status.Snapshot();
        Assert.True(snapshot.Required);
        Assert.Equal("DbFallback", snapshot.Mode);
        Assert.False(snapshot.Connected);
    }

    [Fact]
    public void RedisConnectionRecoveryUpdatesHealthState()
    {
        var status = new RedisRuntimeStatus(required: false);

        status.MarkRedis(connected: false);
        Assert.Equal("RedisReconnecting", status.Snapshot().Mode);

        status.MarkRedisConnected();
        var snapshot = status.Snapshot();
        Assert.Equal("Redis", snapshot.Mode);
        Assert.True(snapshot.Configured);
        Assert.True(snapshot.Connected);
        Assert.Null(snapshot.FailureReason);
    }
}
