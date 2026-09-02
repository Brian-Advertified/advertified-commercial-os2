using Advertified.Commercial.Infrastructure.Inventory;

namespace Advertified.Commercial.Api;

internal static class InventoryEmbeddingRegistration
{
    internal static void AddInventoryEmbeddings(this WebApplicationBuilder builder)
    {
        var settings = builder.Configuration
            .GetSection(InventoryEmbeddingOptions.SectionName)
            .Get<InventoryEmbeddingOptions>() ?? new InventoryEmbeddingOptions();
        EnsureEnvironmentIsSafe(builder, settings);
        builder.Services.AddOptions<InventoryEmbeddingOptions>()
            .Bind(builder.Configuration.GetSection(InventoryEmbeddingOptions.SectionName))
            .Validate(InventoryEmbeddingOptions.IsValid,
                "The inventory embedding provider policy is invalid.")
            .ValidateOnStart();
        builder.Services.AddHttpClient<HttpInventoryEmbeddingGenerator>(ConfigureClient);
        builder.Services.AddScoped<IInventoryEmbeddingGenerator>(serviceProvider =>
            settings.Mode switch
            {
                InventoryEmbeddingOptions.DeterministicMode =>
                    new DeterministicInventoryEmbeddingGenerator(),
                InventoryEmbeddingOptions.BedrockHttpMode =>
                    serviceProvider.GetRequiredService<HttpInventoryEmbeddingGenerator>(),
                _ => new DisabledInventoryEmbeddingGenerator(),
            });
    }

    private static void EnsureEnvironmentIsSafe(
        WebApplicationBuilder builder,
        InventoryEmbeddingOptions settings)
    {
        if (settings.Mode == InventoryEmbeddingOptions.DeterministicMode &&
            !builder.Environment.IsDevelopment() &&
            !builder.Environment.IsEnvironment("Test"))
        {
            throw new InvalidOperationException(
                "Deterministic inventory embeddings are restricted to development and test.");
        }
        var budget = builder.Environment.IsProduction()
            ? InventoryEmbeddingOptions.ProductionBudgetUsdMicros
            : InventoryEmbeddingOptions.StagingBudgetUsdMicros;
        if (settings.Mode == InventoryEmbeddingOptions.BedrockHttpMode &&
            settings.MonthlyBudgetUsdMicros > budget)
        {
            throw new InvalidOperationException(
                "Live inventory embeddings exceed the governed environment budget.");
        }
    }

    private static void ConfigureClient(IServiceProvider serviceProvider, HttpClient client)
    {
        var settings = serviceProvider.GetRequiredService<
            Microsoft.Extensions.Options.IOptions<InventoryEmbeddingOptions>>().Value;
        client.BaseAddress = new Uri(settings.BaseUrl, UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(30);
        if (!string.IsNullOrWhiteSpace(settings.ServiceKey))
        {
            client.DefaultRequestHeaders.Add("X-Advertified-Service-Key", settings.ServiceKey);
        }
    }
}
