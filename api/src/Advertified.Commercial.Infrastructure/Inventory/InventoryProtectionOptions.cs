namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed class InventoryProtectionOptions
{
    public const int MaximumSupportedSourceBytes = 100 * 1024 * 1024;
    public const string SectionName = "InventoryProtection";
    public const string InMemoryMode = "InMemory";
    public const string MinioMode = "Minio";
    public const string DeterministicScanner = "Deterministic";
    public const string ClamAvScanner = "ClamAv";

    public string ObjectStoreMode { get; init; } = InMemoryMode;
    public string ScannerMode { get; init; } = DeterministicScanner;
    public int MaximumSourceBytes { get; init; } = MaximumSupportedSourceBytes;
    public string Endpoint { get; init; } = "localhost:59000";
    public string AccessKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public string Bucket { get; init; } = "advertified-inventory";
    public bool UseTls { get; init; }
    public string ClamAvHost { get; init; } = "localhost";
    public int ClamAvPort { get; init; } = 3310;

    public static bool HasSupportedObjectStore(InventoryProtectionOptions options) =>
        options.ObjectStoreMode is InMemoryMode or MinioMode;

    public static bool HasSupportedScanner(InventoryProtectionOptions options) =>
        options.ScannerMode is DeterministicScanner or ClamAvScanner;

    public static bool HasSupportedSourceLimit(InventoryProtectionOptions options) =>
        options.MaximumSourceBytes is > 0 and <= MaximumSupportedSourceBytes;

    public static bool HasCompleteMinioConfiguration(InventoryProtectionOptions options) =>
        options.ObjectStoreMode != MinioMode ||
        (!string.IsNullOrWhiteSpace(options.Endpoint) &&
         !string.IsNullOrWhiteSpace(options.AccessKey) &&
         !string.IsNullOrWhiteSpace(options.SecretKey));

    public static bool HasCompleteClamAvConfiguration(InventoryProtectionOptions options) =>
        options.ScannerMode != ClamAvScanner ||
        (!string.IsNullOrWhiteSpace(options.ClamAvHost) &&
         options.ClamAvPort is >= 1 and <= 65535);
}
