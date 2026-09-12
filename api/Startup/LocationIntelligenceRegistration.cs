using Advertified.Commercial.Application.LocationIntelligence;
using Advertified.Commercial.Infrastructure.LocationIntelligence;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Api.Startup;

public static class LocationIntelligenceRegistration
{
    public static IServiceCollection AddLocationIntelligence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<LocationDiscoveryOptions>(
            configuration.GetSection(LocationDiscoveryOptions.SectionName));
        services.Configure<PoiDiscoveryOptions>(
            configuration.GetSection(PoiDiscoveryOptions.SectionName));

        services.AddHttpClient(nameof(NominatimLocationDiscoveryProvider))
            .ConfigurePrimaryHttpMessageHandler(() =>
                new HttpClientHandler { AllowAutoRedirect = false });
        services.AddHttpClient(nameof(OverpassPoiDiscoveryProvider))
            .ConfigurePrimaryHttpMessageHandler(() =>
                new HttpClientHandler { AllowAutoRedirect = false });

        services.AddSingleton<ILocationDiscoveryProvider>(provider =>
            new NominatimLocationDiscoveryProvider(
                provider.GetRequiredService<IHttpClientFactory>()
                    .CreateClient(nameof(NominatimLocationDiscoveryProvider)),
                provider.GetRequiredService<IOptions<LocationDiscoveryOptions>>(),
                provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IPoiDiscoveryProvider>(provider =>
            new OverpassPoiDiscoveryProvider(
                provider.GetRequiredService<IHttpClientFactory>()
                    .CreateClient(nameof(OverpassPoiDiscoveryProvider)),
                provider.GetRequiredService<IOptions<PoiDiscoveryOptions>>(),
                provider.GetRequiredService<TimeProvider>()));

        services.AddScoped<LocationResearchExecutor>();
        services.AddScoped<ILocationIntelligenceService, LocationIntelligenceService>();
        return services;
    }
}
