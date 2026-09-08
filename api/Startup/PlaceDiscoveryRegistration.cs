using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Infrastructure.Inventory;

namespace Advertified.Commercial.Api.Startup;

public static class PlaceDiscoveryRegistration
{
    public static IServiceCollection AddPlaceDiscovery(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<PlaceDiscoveryOptions>(configuration.GetSection(PlaceDiscoveryOptions.SectionName));
        services.AddHttpClient(nameof(NominatimPlaceDiscoveryProvider))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddSingleton<IPlaceDiscoveryProvider>(provider => new NominatimPlaceDiscoveryProvider(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(NominatimPlaceDiscoveryProvider)),
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<PlaceDiscoveryOptions>>(),
            provider.GetRequiredService<TimeProvider>()));
        return services;
    }
}
