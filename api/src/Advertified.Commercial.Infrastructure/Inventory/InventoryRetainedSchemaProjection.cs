using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventoryRetainedSchemaProjection
{
    internal static bool HasRetainedLineage(
        InventoryExtractionDocument document) =>
        document.SchemaDiscoveryFailure is null &&
        (document.DiscoveredSchema is not null ||
         document.SourceElements is { Count: > 0 });

    internal static InventoryExtractionResult Replay(
        string sourceHash,
        string providerJson,
        InventoryExtractionDocument retained,
        string adapterVersion)
    {
        // Replay immutable Python bindings or a validated historical schema.
        var failure = retained.SchemaDiscoveryFailure;
        if (retained.DiscoveredSchema is not null &&
            (retained.DiscoveredSchema.SourceHash != sourceHash ||
             retained.Rows.Any(row => row.DiscoveredFields is null)))
        {
            throw new InventoryExtractionUnavailableException();
        }
        if (!HasRetainedLineage(retained))
        {
            failure ??=
                "The retained document has no source-bound interpretation.";
        }
        return InventoryExtractionContract.Create(
            "docling",
            adapterVersion,
            retained.SchemaVersion,
            sourceHash,
            providerJson,
            failure is null ? retained.Rows : [],
            retained.DiscoveredSchema,
            failure,
            retained.SourceAccounting,
            retained.DeduplicationDecisions,
            retained.SourceElements,
            retained.ProjectionWarnings);
    }
}
