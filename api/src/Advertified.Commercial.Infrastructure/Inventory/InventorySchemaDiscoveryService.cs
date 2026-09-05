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
        var request = new InventorySchemaDiscoveryRequest(InventorySchemaValidation.ProtocolVersion,
            document.SourceHash, document.StructureHash, document.Structures.Select(Sample).ToArray(),
            meanings, governedCodes, executionContext);
        var schema = await interpreter.DiscoverAsync(request, cancellationToken);
        InventorySchemaValidation.Validate(document, schema, meanings, governedCodes);
        return schema;
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
