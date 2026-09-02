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
        InventoryExtractedRow[] rows =
        [
            new(1, $"fixture:{request.FileName}#record=1",
                new Dictionary<string, string>()),
        ];
        var providerJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            fixture = "empty-review-candidate",
            request.DocumentClass,
            rows,
        });
        return Task.FromResult(InventoryExtractionContract.Create(
            "advertified-deterministic-fixture",
            "1.0.0",
            InventoryExtractionOptions.CurrentSchemaVersion,
            request.SourceHash,
            providerJson,
            rows));
    }
}
