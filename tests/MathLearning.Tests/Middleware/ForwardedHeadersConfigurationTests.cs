using MathLearning.Api.Startup;
using Microsoft.Extensions.Configuration;

namespace MathLearning.Tests.Middleware;

public sealed class ForwardedHeadersConfigurationTests
{
    [Fact]
    public void CreateOptions_LoadsKnownProxiesAndNetworksFromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownProxies:0"] = "127.0.0.1",
                ["ForwardedHeaders:KnownNetworks:0"] = "10.0.0.0/8",
                ["ForwardedHeaders:KnownNetworks:1"] = "fdaa::/48"
            })
            .Build();

        var options = ForwardedHeadersConfiguration.CreateOptions(configuration);

        Assert.Contains(options.KnownProxies, ip => ip.ToString() == "127.0.0.1");
        Assert.Equal(2, options.KnownNetworks.Count);
    }

    [Fact]
    public void CreateOptions_WhenUnset_DefaultsToLoopbackOnly()
    {
        var configuration = new ConfigurationBuilder().Build();

        var options = ForwardedHeadersConfiguration.CreateOptions(configuration);

        Assert.Contains(options.KnownProxies, ip => System.Net.IPAddress.IsLoopback(ip));
    }
}
