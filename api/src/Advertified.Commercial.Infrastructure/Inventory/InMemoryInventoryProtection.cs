using System.Collections.Concurrent;
using System.Text;
using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed class InMemoryInventoryObjectStore : IInventoryObjectStore
{
    private readonly ConcurrentDictionary<string, byte[]> objects = new();

    public Task PutAsync(
        string objectKey,
        ReadOnlyMemory<byte> content,
        string mediaType,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        objects.AddOrUpdate(
            objectKey,
            content.ToArray(),
            (_, existing) => existing.AsSpan().SequenceEqual(content.Span)
                ? existing
                : throw new InvalidOperationException("An immutable object key was reused."));
        return Task.CompletedTask;
    }

    public Task<byte[]> ReadAsync(
        string objectKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(objects.TryGetValue(objectKey, out var content)
            ? content.ToArray()
            : throw new FileNotFoundException("The protected inventory source was not found."));
    }
}

public sealed class DeterministicInventoryMalwareScanner : IInventoryMalwareScanner
{
    private const string EicarMarker = "EICAR-STANDARD-ANTIVIRUS-TEST-FILE";

    public Task<MalwareScanResult> ScanAsync(
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var text = Encoding.ASCII.GetString(content.Span);
        var clean = !text.Contains(EicarMarker, StringComparison.Ordinal);
        return Task.FromResult(new MalwareScanResult(
            clean,
            clean ? null : "EICAR-Test-Signature"));
    }
}
