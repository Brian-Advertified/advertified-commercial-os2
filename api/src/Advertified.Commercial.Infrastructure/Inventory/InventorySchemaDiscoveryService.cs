using System.Text.Json;
using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed class InventorySchemaDiscoveryService(IInventorySchemaInterpreter interpreter)
{
    // Representative header/context plus distributed samples, not one call per row.
    private const int ContextRows = 8;
    private const int DistributedRows = 12;

    public async Task<DiscoveredInventorySchema> DiscoverAsync(InventoryDocumentStructure document,
        IReadOnlySet<string> meanings, IReadOnlyDictionary<string, IReadOnlySet<string>> governedCodes,
        CancellationToken cancellationToken, InventorySchemaExecutionContext? executionContext = null)
    {
        InventorySchemaValidation.ValidateStructure(document);
        if (document.Structures.Count == 1)
            return await DiscoverSingleAsync(document, meanings, governedCodes,
                executionContext, cancellationToken);
        var sections = new List<DiscoveredInventorySchema>();
        foreach (var structure in document.Structures)
        {
            var section = new InventoryDocumentStructure(document.SourceHash,
                SectionHash(document, structure), [structure]);
            sections.Add(await DiscoverSingleAsync(section, meanings,
                governedCodes, executionContext, cancellationToken));
        }
        var schema = Aggregate(document, sections);
        InventorySchemaValidation.Validate(document, schema, meanings, governedCodes);
        return schema;
    }

    private async Task<DiscoveredInventorySchema> DiscoverSingleAsync(
        InventoryDocumentStructure document,
        IReadOnlySet<string> meanings,
        IReadOnlyDictionary<string, IReadOnlySet<string>> governedCodes,
        InventorySchemaExecutionContext? executionContext,
        CancellationToken cancellationToken)
    {
        var request = new InventorySchemaDiscoveryRequest(InventorySchemaValidation.ProtocolVersion,
            document.SourceHash, document.StructureHash, document.Structures.Select(Sample).ToArray(),
            meanings, governedCodes, executionContext);
        var schema = await interpreter.DiscoverAsync(request, cancellationToken);
        InventorySchemaValidation.Validate(document, schema, meanings, governedCodes);
        return schema;
    }

    private static string SectionHash(
        InventoryDocumentStructure document,
        InventorySourceStructure structure) => InventoryExtractionContract.Hash(
        JsonSerializer.Serialize(new
        {
            document.StructureHash,
            Structure = structure,
        }, InventoryRowMapper.StoredJson));

    private static DiscoveredInventorySchema Aggregate(
        InventoryDocumentStructure document,
        IReadOnlyList<DiscoveredInventorySchema> sections)
    {
        var provenance = sections.Select(section => section.Provenance).ToArray();
        var requests = provenance.Select(item => item.ProviderRequestId)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
        var costs = provenance.Select(item => item.CostUsdMicros).ToArray();
        return new DiscoveredInventorySchema(
            InventorySchemaValidation.ProtocolVersion,
            document.SourceHash,
            document.StructureHash,
            sections.SelectMany(section => section.Records).ToArray(),
            sections.Min(section => section.Confidence),
            sections.SelectMany(section => section.Warnings).ToArray(),
            new InventorySchemaProvenance(
                Common(provenance.Select(item => item.Interpreter)),
                Common(provenance.Select(item => item.ConfigurationVersion)),
                CommonOptional(provenance.Select(item => item.Model)),
                requests.Length == 0 ? null : string.Join(',', requests),
                provenance.Sum(item => item.AiCalls),
                costs.All(item => item is null) ? null : costs.Sum(item => item ?? 0)),
            CommonCorrection(sections.Select(section => section.Correction)));
    }

    private static string Common(IEnumerable<string> values)
    {
        var distinct = values.Distinct(StringComparer.Ordinal).ToArray();
        return distinct.Length == 1 ? distinct[0] : "MULTIPLE_SECTION_INTERPRETERS";
    }

    private static string? CommonOptional(IEnumerable<string?> values)
    {
        var distinct = values.Distinct(StringComparer.Ordinal).ToArray();
        return distinct.Length == 1 ? distinct[0] : null;
    }

    private static InventoryInterpretationCorrection? CommonCorrection(
        IEnumerable<InventoryInterpretationCorrection?> values)
    {
        var distinct = values.Distinct().ToArray();
        return distinct.Length == 1 ? distinct[0] : null;
    }

    private static InventorySourceStructure Sample(InventorySourceStructure structure)
    {
        var rows = structure.Cells.Select(cell => cell.Row).Distinct().Order().ToArray();
        if (rows.Length <= ContextRows + DistributedRows) return structure;
        var selected = rows.Take(ContextRows).ToHashSet();
        for (var index = 0; index < DistributedRows; index++)
            selected.Add(rows[ContextRows + (rows.Length - ContextRows - 1) * index / (DistributedRows - 1)]);
        return structure with { Cells = structure.Cells.Where(cell => selected.Contains(cell.Row)).ToArray() };
    }
}
