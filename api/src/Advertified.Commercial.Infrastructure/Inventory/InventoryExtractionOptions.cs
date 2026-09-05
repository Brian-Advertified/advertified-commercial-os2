namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed class InventoryExtractionOptions
{
    public const string SectionName = "InventoryExtraction";
    public const string DoclingMode = "Docling";
    public const string DeterministicMode = "Deterministic";
    public const string PinnedAdapterVersion =
        "docling-serve/1.30.0;docling/2.118.0;advertified-projection/3.9.0;" +
        NativeOfficeInventoryProjection.AdapterVersion + ";" +
        DoclingInventoryExtractionAdapter.EmbeddedImageProjectionVersion;
    public const string CurrentSchemaVersion = "advertified.inventory-extraction.v3";

    public string Mode { get; init; } = DeterministicMode;
    public string BaseUrl { get; init; } = "http://localhost:55001";
    public string ApiKey { get; init; } = string.Empty;
    public int TimeoutSeconds { get; init; } = 180;

    public static bool HasSupportedMode(InventoryExtractionOptions options) =>
        options.Mode is DoclingMode or DeterministicMode;

    public static bool HasCompleteDoclingConfiguration(InventoryExtractionOptions options) =>
        options.Mode != DoclingMode ||
        (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _) &&
         !string.IsNullOrWhiteSpace(options.ApiKey) &&
         options.TimeoutSeconds is >= 10 and <= 600);
}
