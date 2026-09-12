namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed class InventoryProtectionOptions
{
    public const int MaximumSupportedSourceBytes = 100 * 1024 * 1024;
    public const string SectionName = "InventoryProtection";
    public const string InMemoryMode = "InMemory";
    public const string MinioMode = "Minio";
    public const string AwsS3Mode = "AwsS3";
    public const string DeterministicScanner = "Deterministic";
    public const string ExternalVerdictScanner = "ExternalVerdict";

    public string ObjectStoreMode { get; init; } = InMemoryMode;
    public string ScannerMode { get; init; } = DeterministicScanner;
    public int MaximumSourceBytes { get; init; } = MaximumSupportedSourceBytes;
    public string Endpoint { get; init; } = "localhost:59000";
    public string AccessKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public string Bucket { get; init; } = "advertified-inventory";
    public string AwsRegion { get; init; } = "af-south-1";
    public bool UseTls { get; init; }

    public static bool HasSupportedObjectStore(InventoryProtectionOptions options) =>
        options.ObjectStoreMode is InMemoryMode or MinioMode or AwsS3Mode;

    public static bool HasSupportedScanner(InventoryProtectionOptions options) =>
        options.ScannerMode is DeterministicScanner or ExternalVerdictScanner;

    public static bool HasSupportedSourceLimit(InventoryProtectionOptions options) =>
        options.MaximumSourceBytes is > 0 and <= MaximumSupportedSourceBytes;

    public static bool HasCompleteMinioConfiguration(InventoryProtectionOptions options) =>
        options.ObjectStoreMode != MinioMode ||
        (!string.IsNullOrWhiteSpace(options.Endpoint) &&
         !string.IsNullOrWhiteSpace(options.AccessKey) &&
         !string.IsNullOrWhiteSpace(options.SecretKey));

    public static bool HasCompleteAwsS3Configuration(InventoryProtectionOptions options) =>
        options.ObjectStoreMode != AwsS3Mode ||
        (!string.IsNullOrWhiteSpace(options.Bucket) &&
         !string.IsNullOrWhiteSpace(options.AwsRegion) &&
         options.AwsRegion.Length <= 50 &&
         options.AwsRegion.All(character => char.IsLetterOrDigit(character) || character == '-'));

    public static bool HasCompatibleScannerStorage(InventoryProtectionOptions options) =>
        options.ScannerMode != ExternalVerdictScanner || options.ObjectStoreMode == AwsS3Mode;
}
