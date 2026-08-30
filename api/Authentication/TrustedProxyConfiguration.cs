using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Advertified.Commercial.Api.Authentication;

internal static class TrustedProxyConfiguration
{
    private const string KnownProxiesKey = "ReverseProxy:KnownProxies";
    private const string KnownNetworksKey = "ReverseProxy:KnownNetworks";

    internal static IServiceCollection AddTrustedProxyHeaders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var proxies = Values(configuration, KnownProxiesKey)
            .Select(IPAddress.Parse).ToArray();
        var networks = Values(configuration, KnownNetworksKey)
            .Select(System.Net.IPNetwork.Parse).ToArray();
        return services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            foreach (var proxy in proxies) options.KnownProxies.Add(proxy);
            foreach (var network in networks) options.KnownIPNetworks.Add(network);
        });
    }

    internal static bool HasExplicitTrustBoundary(IConfiguration configuration) =>
        Values(configuration, KnownProxiesKey).Length > 0 ||
        Values(configuration, KnownNetworksKey).Length > 0;

    private static string[] Values(IConfiguration configuration, string key) =>
        configuration.GetSection(key).Get<string[]>()?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim()).ToArray() ?? [];
}
