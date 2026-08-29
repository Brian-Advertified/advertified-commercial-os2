using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed class DeterministicInventoryExtractionAdapter :
    IInventoryDocumentExtractionAdapter
{
    public Task<InventoryExtractionResult> ExtractAsync(
        InventoryExtractionRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var rows = InventorySourceExtractor.Extract(request.DocumentClass, request.Content);
        var json = JsonSerializer.Serialize(new { rows });
        var outputHash = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        return Task.FromResult(new InventoryExtractionResult(
            "advertified-deterministic-fixture",
            "1.0.0",
            InventoryExtractionOptions.CurrentSchemaVersion,
            request.SourceHash,
            json,
            outputHash,
            rows));
    }
}
