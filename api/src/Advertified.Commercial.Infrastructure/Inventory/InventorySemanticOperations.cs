namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventorySemanticOperations
{
    internal const string SemanticEnrichment =
        "SEMANTIC_ENRICHMENT";

    internal static bool IsSupported(string value) =>
        value == SemanticEnrichment;
}
