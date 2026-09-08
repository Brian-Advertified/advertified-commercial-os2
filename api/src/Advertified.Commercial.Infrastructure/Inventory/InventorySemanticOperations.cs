namespace Advertified.Commercial.Infrastructure.Inventory;

public static class InventorySemanticOperations
{
    public const string SourceTranscription =
        "SOURCE_TRANSCRIPTION";
    public const string SemanticEnrichment =
        "SEMANTIC_ENRICHMENT";

    internal static bool IsSupported(string value) =>
        value is SourceTranscription or SemanticEnrichment;
}
