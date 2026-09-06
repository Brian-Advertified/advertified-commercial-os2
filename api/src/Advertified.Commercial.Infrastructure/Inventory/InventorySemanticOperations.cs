namespace Advertified.Commercial.Infrastructure.Inventory;

public static class InventorySemanticOperations
{
    public const string SemanticEnrichment =
        "SEMANTIC_ENRICHMENT";

    internal static bool IsSupported(string value) =>
        value == SemanticEnrichment;
}
