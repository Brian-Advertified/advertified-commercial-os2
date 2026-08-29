using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Infrastructure.Inventory;

namespace Advertified.Commercial.Api;

internal static class InventoryExtractionRegistration
{
    internal static void AddInventoryExtraction(
        this WebApplicationBuilder builder,
        InventoryExtractionOptions settings)
    {
        if (settings.Mode == InventoryExtractionOptions.DeterministicMode &&
            !builder.Environment.IsDevelopment() &&
            !builder.Environment.IsEnvironment("Test"))
        {
            throw new InvalidOperationException(
                "Deterministic document extraction is restricted to development and test.");
        }
        builder.Services.AddOptions<InventoryExtractionOptions>()
            .Bind(builder.Configuration.GetSection(InventoryExtractionOptions.SectionName))
            .Validate(InventoryExtractionOptions.HasSupportedMode,
                "The inventory extraction mode is invalid.")
            .Validate(InventoryExtractionOptions.HasCompleteDoclingConfiguration,
                "Docling extraction requires an absolute URL, API key and valid timeout.")
            .ValidateOnStart();
        builder.Services.AddHttpClient<DoclingInventoryExtractionAdapter>(
            (serviceProvider, client) => ConfigureClient(serviceProvider, client));
        builder.Services.AddScoped<IInventoryDocumentExtractionAdapter>(serviceProvider =>
            settings.Mode == InventoryExtractionOptions.DoclingMode
                ? serviceProvider.GetRequiredService<DoclingInventoryExtractionAdapter>()
                : new DeterministicInventoryExtractionAdapter());
    }

    private static void ConfigureClient(IServiceProvider serviceProvider, HttpClient client)
    {
        var options = serviceProvider.GetRequiredService<
            Microsoft.Extensions.Options.IOptions<InventoryExtractionOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    }
}
