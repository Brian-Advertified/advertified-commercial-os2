namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed class InventoryExtractionOptions
{
    public const string SectionName = "InventoryExtraction";
    public const string DeterministicMode = "Deterministic";
    public const string PinnedAdapterVersion =
        NativeInventoryExtractionAdapter.Version;
    public const string CurrentSchemaVersion = "1.0.0";

    public string Mode { get; init; } = DeterministicMode;

    public static bool HasSupportedMode(InventoryExtractionOptions options) =>
        options.Mode == DeterministicMode;
}
