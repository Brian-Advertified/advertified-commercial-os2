using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Infrastructure.Opportunity;

namespace Advertified.Commercial.Infrastructure.Inventory;

// Pipeline step between structural extraction and normalization. It discovers
// the document schema once per document, projects every record through the
// discovered schema with full raw evidence, and marks the extraction with the
// discovered schema. Failure retains the raw artifact for document-level review;
// legacy commercial guesses must not replace a rejected interpretation.
public sealed class InventorySchemaExtractionStep(
    InventorySchemaDiscoveryService discovery)
{
    internal async Task<InventoryExtractionResult> ApplyAsync(
        InventoryExtractionResult extraction,
        InventoryCodeSets codes,
        InventorySchemaExecutionContext? executionContext,
        CancellationToken cancellationToken)
    {
        if (extraction.Document.DiscoveredSchema is not null)
        {
            return extraction;
        }
        try
        {
            var document = InventoryDocumentStructureReader.Read(
                extraction.SourceHash, extraction.ProviderJson);
            if (document.Structures.Count == 0 || document.ExtractionGaps is { Count: > 0 })
                throw new InventorySchemaRejectedException("No supported source structures were retained.");
            var meanings = InventoryCandidateNormalizer.CanonicalMeanings;
            var governed = GovernedCodes(codes);
            var schema = await discovery.DiscoverAsync(
                document, meanings, governed, cancellationToken, executionContext);
            var rows = InventorySchemaBatchProjection.Project(
                document, schema, meanings, governed);
            return InventoryExtractionContract.Create(
                extraction.AdapterCode,
                extraction.AdapterVersion,
                extraction.SchemaVersion,
                extraction.SourceHash,
                extraction.ProviderJson,
                rows,
                schema);
        }
        catch (Exception exception) when (exception is InventorySchemaRejectedException ||
            IsProviderInterruption(exception, cancellationToken))
        {
            return InventoryExtractionContract.Create(extraction.AdapterCode, extraction.AdapterVersion,
                extraction.SchemaVersion, extraction.SourceHash, extraction.ProviderJson, [],
                schemaDiscoveryFailure: "The document could not be interpreted safely. Review the retained source before retrying.");
        }
    }

    private static bool IsProviderInterruption(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException or AgentRuntimeRejectedException or
            InventorySemanticReconciliationRequiredException or InventorySemanticResultRejectedException or
            System.Text.Json.JsonException ||
        exception is OperationCanceledException && !cancellationToken.IsCancellationRequested;

    internal static IReadOnlyDictionary<string, IReadOnlySet<string>> GovernedCodes(
        InventoryCodeSets codes) => new Dictionary<string, IReadOnlySet<string>>(
        StringComparer.Ordinal)
    {
        ["channel"] = codes.Channels,
        ["product_type"] = codes.ProductTypes,
        ["rate_type"] = codes.RateTypes,
        ["currency"] = codes.Currencies,
        ["availability"] = codes.Availability,
    };
}
