using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventorySchemaValidation
{
    internal const string ProtocolVersion = "inventory-schema/1.0";
    // Parser resource budgets, never commercial defaults or acceptance confidence thresholds.
    internal const int MaximumCells = 1_000_000;
    internal const int MaximumStructures = 256;
    internal const int MaximumMappings = 256;
    internal const int MaximumCellCharacters = 24_000;

    internal static void ValidateStructure(InventoryDocumentStructure document)
    {
        if (document.Structures.Count > MaximumStructures ||
            document.Structures.Sum(item => (long)item.Cells.Count) > MaximumCells ||
            document.Structures.Select(item => item.Id).Distinct().Count() != document.Structures.Count)
            throw new InventorySchemaRejectedException("Document structural budget exceeded or structure identifiers are repeated.");
        foreach (var structure in document.Structures)
        {
            if (structure.Cells.Any(cell => cell.Row < 0 || cell.Row > MaximumCells ||
                    cell.Column < 0 || cell.Column > MaximumMappings || cell.RawText.Length > MaximumCellCharacters ||
                    string.IsNullOrWhiteSpace(cell.Locator)) ||
                structure.Cells.Select(cell => cell.Locator).Distinct().Count() != structure.Cells.Count ||
                structure.Cells.Select(cell => (cell.Row, cell.Column)).Distinct().Count() != structure.Cells.Count)
                throw new InventorySchemaRejectedException("Source positions are invalid, repeated or outside parser limits.");
        }
    }

    internal static void Validate(InventoryDocumentStructure document, DiscoveredInventorySchema schema,
        IReadOnlySet<string> meanings, IReadOnlyDictionary<string, IReadOnlySet<string>> codes)
    {
        ValidateStructure(document);
        if (schema.ProtocolVersion != ProtocolVersion || schema.SourceHash != document.SourceHash ||
            schema.StructureHash != document.StructureHash || schema.Confidence is < 0 or > 1 ||
            schema.Records.Count > MaximumStructures || schema.Provenance.AiCalls < 0 ||
            schema.Provenance.CostUsdMicros < 0)
            throw new InventorySchemaRejectedException("Schema identity, provenance or resource bounds are invalid.");
        var structures = document.Structures.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var mapped = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in schema.Records)
        {
            if (!structures.TryGetValue(record.SourceStructure, out var structure) || !mapped.Add(structure.Id))
                throw new InventorySchemaRejectedException("Schema references a missing or repeated structure.");
            ValidateRecord(record, structure, meanings, codes);
        }
        // Omission must be represented as unresolved evidence, never silent deletion.
        if (mapped.Count != structures.Count)
            throw new InventorySchemaRejectedException("Every extracted structure must be accounted for.");
    }

    private static void ValidateRecord(InventoryRecordSchema record, InventorySourceStructure structure,
        IReadOnlySet<string> meanings, IReadOnlyDictionary<string, IReadOnlySet<string>> codes)
    {
        var boundary = record.RecordBoundary;
        if (boundary.FirstRow < 0 || boundary.LastRow < boundary.FirstRow ||
            boundary.RowsPerRecord is < 1 or > MaximumCells ||
            structure.Cells.Count == 0 || boundary.LastRow > structure.Cells.Max(cell => cell.Row) ||
            boundary.ExcludedRows.Any(row => row < boundary.FirstRow || row > boundary.LastRow))
            throw new InventorySchemaRejectedException("Record boundaries are not grounded in the structure.");
        var mappings = record.FieldMappings.Concat(record.SupplierMetadataMappings).Concat(record.AssetMappings).ToArray();
        if (mappings.Length > MaximumMappings)
            throw new InventorySchemaRejectedException("Schema field budget exceeded.");
        var cells = structure.Cells.ToDictionary(cell => cell.Locator, StringComparer.Ordinal);
        foreach (var mapping in mappings)
        {
            if (mapping.SourceStructure != structure.Id || mapping.SourceColumn is < 0 or > MaximumMappings || mapping.RowOffset < 0 ||
                mapping.RowOffset >= boundary.RowsPerRecord || mapping.Confidence is < 0 or > 1 ||
                !cells.TryGetValue(mapping.SourceLocation, out var label) || label.RawText != mapping.SourceLabel ||
                mapping.CanonicalMeaning is { } meaning && !meanings.Contains(meaning))
                throw new InventorySchemaRejectedException("Field mapping target or source label is invalid.");
            ValidateCitation(mapping, cells, codes);
            if (mapping.IsDocumentMetadata && mapping.InterpretedCode is null &&
                (mapping.ValueSourceLocation is null || !cells.ContainsKey(mapping.ValueSourceLocation)))
                throw new InventorySchemaRejectedException("Document metadata must reference an existing source value.");
        }
    }

    private static void ValidateCitation(InventorySchemaFieldMapping mapping,
        Dictionary<string, InventorySourceCell> cells,
        IReadOnlyDictionary<string, IReadOnlySet<string>> codes)
    {
        if (mapping.Evidence.Count == 0 || mapping.Evidence.Any(citation =>
                string.IsNullOrWhiteSpace(citation.QuotedText) ||
                !cells.TryGetValue(citation.SourceLocator, out var source) ||
                !source.RawText.Contains(citation.QuotedText, StringComparison.Ordinal)))
            throw new InventorySchemaRejectedException("Schema interpretation has unsupported evidence.");
        if (mapping.InterpretedCode is { } code && (mapping.CanonicalMeaning is null ||
                !codes.TryGetValue(mapping.CanonicalMeaning, out var allowed) || !allowed.Contains(code)))
            throw new InventorySchemaRejectedException("Interpretation may classify governed codes, not manufacture source values.");
    }
}
