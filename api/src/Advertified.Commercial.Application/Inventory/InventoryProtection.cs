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
}

public interface IInventoryMalwareScanner
{
    Task<MalwareScanResult> ScanAsync(
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken);
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
