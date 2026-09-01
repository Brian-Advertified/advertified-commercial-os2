using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Infrastructure.Inventory;

namespace Advertified.Commercial.Api;

internal static class InventoryExtractionRegistration
{
    internal static void AddInventoryExtraction(
        this WebApplicationBuilder builder,
        InventoryExtractionOptions settings)
    {
        EnsureEnvironmentIsSafe(builder, settings);
        builder.Services.AddOptions<InventoryExtractionOptions>()
            .Bind(builder.Configuration.GetSection(InventoryExtractionOptions.SectionName))
            .Validate(InventoryExtractionOptions.HasSupportedMode,
                "The inventory extraction mode is invalid.")
            .Validate(InventoryExtractionOptions.HasCompleteDoclingConfiguration,
                "Docling extraction requires an absolute URL, API key and valid timeout.")
            .ValidateOnStart();
        builder.Services.AddHttpClient<DoclingInventoryExtractionAdapter>(
            (serviceProvider, client) => ConfigureClient(serviceProvider, client))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false,
            });
        builder.Services.AddScoped<IInventoryDocumentExtractionAdapter>(serviceProvider =>
            settings.Mode == InventoryExtractionOptions.DoclingMode
                ? serviceProvider.GetRequiredService<DoclingInventoryExtractionAdapter>()
                : new DeterministicInventoryExtractionAdapter());
    }

    private static void EnsureEnvironmentIsSafe(
        WebApplicationBuilder builder,
        InventoryExtractionOptions settings)
    {
        if (builder.Environment.IsDevelopment() ||
            builder.Environment.IsEnvironment("Test"))
        {
            return;
        }
        if (settings.Mode == InventoryExtractionOptions.DeterministicMode)
        {
            throw new InvalidOperationException(
                "Deterministic document extraction is restricted to development and test.");
        }
        if (settings.Mode == InventoryExtractionOptions.DoclingMode &&
            (!Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out var baseUri) ||
             baseUri.Scheme != Uri.UriSchemeHttps ||
             string.IsNullOrWhiteSpace(baseUri.Host) ||
             !string.IsNullOrEmpty(baseUri.UserInfo)))
        {
            throw new InvalidOperationException(
                "Docling document extraction must use an HTTPS URL with a host and no " +
                "embedded credentials outside development and test.");
        }
    }

    private static void ConfigureClient(IServiceProvider serviceProvider, HttpClient client)
    {
        var options = serviceProvider.GetRequiredService<
            Microsoft.Extensions.Options.IOptions<InventoryExtractionOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    }
}
