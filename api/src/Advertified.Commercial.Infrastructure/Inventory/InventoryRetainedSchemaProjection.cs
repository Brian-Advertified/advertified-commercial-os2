using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventoryRetainedSchemaProjection
{
    internal static InventoryExtractionResult Replay(string sourceHash, string providerJson,
        InventoryExtractionDocument retained, string adapterVersion)
    {
        // Replay the immutable raw fields and bindings through the current normalizer.
        // Missing historical discovery requires a separate explicit interpretation action.
        var failure = retained.SchemaDiscoveryFailure;
        if (retained.DiscoveredSchema is null)
            failure ??= "The retained document has no accepted interpretation. Review the source before requesting new interpretation.";
        else if (retained.DiscoveredSchema.SourceHash != sourceHash ||
                 retained.Rows.Any(row => row.DiscoveredFields is null))
            throw new InventoryExtractionUnavailableException();
        return InventoryExtractionContract.Create("docling", adapterVersion, retained.SchemaVersion,
            sourceHash, providerJson, failure is null ? retained.Rows : [], retained.DiscoveredSchema, failure);
    }
}
