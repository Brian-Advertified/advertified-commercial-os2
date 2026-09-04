namespace Advertified.Commercial.Application.Inventory;

public sealed record MalwareScanResult(bool IsClean, string? ThreatName);

public interface IInventoryObjectStore
{
    Task PutAsync(
        string objectKey,
        ReadOnlyMemory<byte> content,
        string mediaType,
        CancellationToken cancellationToken);

    Task<byte[]> ReadAsync(string objectKey, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken);
}

public interface IInventoryMalwareScanner
{
    Task<MalwareScanResult> ScanAsync(
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken);
}

public sealed record InventoryExtractionRequest(
    string FileName,
    string MediaType,
    string DocumentClass,
    string SourceHash,
    byte[] Content);

public sealed record InventoryExtractedRow(
    int Number,
    string Locator,
    IReadOnlyDictionary<string, string> Values,
    string? ExtractionMethod = null,
    decimal? Confidence = null,
    IReadOnlyDictionary<string, string>? FieldLocators = null,
    IReadOnlyDictionary<string, decimal?>? FieldConfidences = null,
    IReadOnlyDictionary<string, string>? FieldEvidenceBases = null,
    IReadOnlyDictionary<string, string>? FieldTransformations = null);

public sealed record InventoryExtractionDocument(
    string SchemaVersion,
    IReadOnlyList<InventoryExtractedRow> Rows);

public sealed record InventoryExtractionResult(
    string AdapterCode,
    string AdapterVersion,
    string SourceHash,
    string ProviderJson,
    string ProviderOutputHash,
    string CanonicalJson,
    string CanonicalOutputHash,
    InventoryExtractionDocument Document)
{
    public string SchemaVersion => Document.SchemaVersion;
    public IReadOnlyList<InventoryExtractedRow> Rows => Document.Rows;
}

public interface IInventoryDocumentExtractionAdapter
{
    Task<InventoryExtractionResult> ExtractAsync(
        InventoryExtractionRequest request,
        CancellationToken cancellationToken);
}

public sealed class InventoryExtractionUnavailableException : Exception
{
    public InventoryExtractionUnavailableException() : base("Document extraction is unavailable.")
    {
    }
}

public sealed class UnsafeInventorySourceException : Exception
{
    public UnsafeInventorySourceException() : base("The inventory source is unsafe.")
    {
    }
}

public sealed class InventoryProtectionUnavailableException : Exception
{
    public InventoryProtectionUnavailableException() : base("File protection is unavailable.")
    {
    }
}

public sealed class InventoryPublishBlockedException : Exception
{
    public InventoryPublishBlockedException() : base("Inventory publication is blocked.")
    {
    }
}
