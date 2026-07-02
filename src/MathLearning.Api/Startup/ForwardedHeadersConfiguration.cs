using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace MathLearning.Api.Startup;

public static class ForwardedHeadersConfiguration
{
    public static ForwardedHeadersOptions CreateOptions(IConfiguration configuration)
    {
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor
                | ForwardedHeaders.XForwardedProto
                | ForwardedHeaders.XForwardedHost,
            ForwardLimit = configuration.GetValue<int?>("ForwardedHeaders:ForwardLimit") ?? 2
        };

        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();

        foreach (var proxy in configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
        {
            if (IPAddress.TryParse(proxy, out var ip))
                options.KnownProxies.Add(ip);
        }

        foreach (var cidr in configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [])
        {
            if (TryParseCidr(cidr, out var network, out var prefixLength))
                options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(network, prefixLength));
        }

        if (options.KnownProxies.Count == 0 && options.KnownNetworks.Count == 0)
        {
            options.KnownProxies.Add(IPAddress.Loopback);
            options.KnownProxies.Add(IPAddress.IPv6Loopback);
        }

        return options;
    }

    public static void ValidateProductionTrustBoundary(
        IHostEnvironment environment,
        ForwardedHeadersOptions options)
    {
        if (environment.IsDevelopment() || environment.IsEnvironment("Test"))
            return;

        var hasNonLoopbackProxy = options.KnownProxies.Any(ip => !IPAddress.IsLoopback(ip));
        if (!hasNonLoopbackProxy && options.KnownNetworks.Count == 0)
        {
            Serilog.Log.Warning(
                "ForwardedHeaders has no production proxy trust configured (only loopback). " +
                "Configure ForwardedHeaders:KnownNetworks for Fly/reverse-proxy private networks.");
        }
    }

    private static bool TryParseCidr(string cidr, out IPAddress network, out int prefixLength)
    {
        network = IPAddress.None;
        prefixLength = 0;

        var parts = cidr.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var parsed) || !int.TryParse(parts[1], out prefixLength))
            return false;

        network = parsed;
        return prefixLength >= 0;
    }
}
