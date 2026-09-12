using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventorySchemaProjection
{
    internal static IReadOnlyList<InventoryExtractedRow> Project(
        InventoryDocumentStructure document,
        InventorySchemaProposal proposal,
        IReadOnlyDictionary<string, IReadOnlySet<string>> governedCodes)
    {
        Validate(document, proposal, governedCodes);
        var structures = document.Structures.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var rows = new List<InventoryExtractedRow>();
        foreach (var record in proposal.Records)
            ProjectRecord(InventoryRecordOrientation.Apply(structures[record.SourceStructure], record),
                record, proposal, rows);
        return rows;
    }

    internal static void Validate(
        InventoryDocumentStructure document,
        InventorySchemaProposal proposal,
        IReadOnlyDictionary<string, IReadOnlySet<string>> governedCodes)
    {
        if (proposal.ProtocolVersion != "inventory-schema/1.0" ||
            proposal.SourceHash != document.SourceHash ||
            proposal.StructureHash != document.StructureHash)
            throw new InventorySchemaRejectedException("Schema source identity is invalid.");
        if (proposal.Confidence is < 0 or > 1)
            throw new InventorySchemaRejectedException("Schema confidence is invalid.");
        var structures = document.Structures.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var mapped = proposal.Records.Select(item => item.SourceStructure).ToArray();
        if (mapped.Distinct(StringComparer.Ordinal).Count() != mapped.Length ||
            !mapped.ToHashSet(StringComparer.Ordinal).SetEquals(structures.Keys))
            throw new InventorySchemaRejectedException("Schema must account for each source structure exactly once.");
        foreach (var record in proposal.Records)
            ValidateRecord(InventoryRecordOrientation.Apply(structures[record.SourceStructure], record),
                record, governedCodes);
    }

    private static void ValidateRecord(
        InventorySourceStructure structure,
        InventoryRecordSchema record,
        IReadOnlyDictionary<string, IReadOnlySet<string>> governedCodes)
    {
        var boundary = record.RecordBoundary;
        if (boundary.FirstRow < 0 || boundary.LastRow < boundary.FirstRow ||
            boundary.RowsPerRecord <= 0 || boundary.LastRow - boundary.FirstRow > 1_000_000)
            throw new InventorySchemaRejectedException("Schema record boundary is invalid.");
        if (boundary.ExcludedRows.Distinct().Count() != boundary.ExcludedRows.Count ||
            boundary.ExcludedRows.Any(row => row < boundary.FirstRow || row > boundary.LastRow))
            throw new InventorySchemaRejectedException("Schema excluded rows are invalid.");
        foreach (var row in boundary.ExcludedRows)
            if (boundary.ExclusionReasons?.TryGetValue(row, out var reason) != true ||
                string.IsNullOrWhiteSpace(reason))
                throw new InventorySchemaRejectedException("Every excluded row requires a reason.");
        var cells = structure.Cells.ToDictionary(item => item.Locator, StringComparer.Ordinal);
        var mappings = record.FieldMappings.Concat(record.SupplierMetadataMappings)
            .Concat(record.AssetMappings).ToArray();
        if (mappings.Length > 256)
            throw new InventorySchemaRejectedException("Schema mapping budget exceeded.");
        foreach (var mapping in mappings)
            ValidateMapping(structure, record, mapping, cells, governedCodes);
    }

    private static void ValidateMapping(
        InventorySourceStructure structure,
        InventoryRecordSchema record,
        InventorySchemaFieldMapping mapping,
        Dictionary<string, InventorySourceCell> cells,
        IReadOnlyDictionary<string, IReadOnlySet<string>> governedCodes)
    {
        ValidateSourceBinding(record, mapping, cells);
        ValidateMeaning(mapping);
        ValidateEvidence(mapping, cells);
        ValidateInterpretedCode(mapping, governedCodes);
        ValidateValueSource(structure, mapping, cells);
    }

    private static void ValidateSourceBinding(
        InventoryRecordSchema record,
        InventorySchemaFieldMapping mapping,
        Dictionary<string, InventorySourceCell> cells)
    {
        if (mapping.SourceStructure != record.SourceStructure ||
            !cells.TryGetValue(mapping.SourceLocation, out var label) ||
            label.RawText != mapping.SourceLabel ||
            mapping.SourceColumn < 0 || mapping.SourceColumn > InventoryRecordOrientation.MaximumBindingColumn(record) ||
            mapping.RowOffset < 0 ||
            mapping.RowOffset >= record.RecordBoundary.RowsPerRecord ||
            mapping.Confidence is < 0 or > 1)
            throw new InventorySchemaRejectedException("Schema source binding is invalid.");
    }

    private static void ValidateMeaning(InventorySchemaFieldMapping mapping)
    {
        if (mapping.CanonicalMeaning is { } meaning &&
            !InventoryCandidateNormalizer.CanonicalMeanings.Contains(meaning))
            throw new InventorySchemaRejectedException("Schema canonical meaning is invalid.");
    }

    private static void ValidateEvidence(
        InventorySchemaFieldMapping mapping,
        Dictionary<string, InventorySourceCell> cells)
    {
        if (mapping.Evidence.Count == 0)
            throw new InventorySchemaRejectedException("Schema interpretation evidence is invalid.");
        foreach (var citation in mapping.Evidence)
        {
            if (string.IsNullOrWhiteSpace(citation.QuotedText) ||
                !cells.TryGetValue(citation.SourceLocator, out var source) ||
                !source.RawText.Contains(citation.QuotedText, StringComparison.Ordinal))
                throw new InventorySchemaRejectedException("Schema interpretation evidence is invalid.");
        }
    }

    private static void ValidateInterpretedCode(
        InventorySchemaFieldMapping mapping,
        IReadOnlyDictionary<string, IReadOnlySet<string>> governedCodes)
    {
        if (mapping.InterpretedCode is not { } interpreted)
            return;
        if (mapping.CanonicalMeaning is not { } meaning ||
            !governedCodes.TryGetValue(meaning, out var codes) ||
            !codes.Contains(interpreted))
            throw new InventorySchemaRejectedException("Schema interpreted code is not governed.");
    }

    private static void ValidateValueSource(
        InventorySourceStructure structure,
        InventorySchemaFieldMapping mapping,
        Dictionary<string, InventorySourceCell> cells)
    {
        if (mapping.IsDocumentMetadata)
        {
            if (mapping.InterpretedCode is null &&
                (mapping.ValueSourceLocation is null ||
                 !cells.ContainsKey(mapping.ValueSourceLocation)))
                throw new InventorySchemaRejectedException("Document metadata must reference source evidence.");
            return;
        }
        if (mapping.SourceColumn == 0)
            throw new InventorySchemaRejectedException("Record fields require a source column.");
        if (!structure.Cells.Any(cell => cell.Column == mapping.SourceColumn))
            throw new InventorySchemaRejectedException("Schema source column does not exist.");
    }

    private static void ProjectRecord(
        InventorySourceStructure structure,
        InventoryRecordSchema record,
        InventorySchemaProposal proposal,
        List<InventoryExtractedRow> output)
    {
        var boundary = record.RecordBoundary;
        var excluded = boundary.ExcludedRows.ToHashSet();
        for (var first = boundary.FirstRow; first <= boundary.LastRow; first += boundary.RowsPerRecord)
        {
            var last = Math.Min(boundary.LastRow, first + boundary.RowsPerRecord - 1);
            if (Enumerable.Range(first, last - first + 1).All(excluded.Contains))
                continue;
            var fields = record.FieldMappings.Concat(record.SupplierMetadataMappings)
                .Concat(record.AssetMappings)
                .Select(mapping => ProjectField(structure, mapping, first, last, excluded))
                .Where(field => field is not null)
                .Cast<InventoryDiscoveredField>()
                .ToArray();
            if (fields.Length == 0)
                continue;
            output.Add(new InventoryExtractedRow(
                output.Count + 1,
                $"{structure.Id}:record={first}",
                new Dictionary<string, string>(StringComparer.Ordinal),
                Confidence: proposal.Confidence,
                DiscoveredFields: fields,
                SchemaWarnings: proposal.Warnings));
        }
    }

    private static InventoryDiscoveredField? ProjectField(
        InventorySourceStructure structure,
        InventorySchemaFieldMapping mapping,
        int recordFirstRow,
        int recordLastRow,
        HashSet<int> excludedRows)
    {
        InventorySourceCell? valueCell;
        if (mapping.IsDocumentMetadata)
        {
            valueCell = mapping.ValueSourceLocation is { } locator
                ? structure.Cells.Single(item => item.Locator == locator)
                : structure.Cells.Single(item => item.Locator == mapping.SourceLocation);
        }
        else
        {
            var row = recordFirstRow + mapping.RowOffset;
            if (row > recordLastRow || excludedRows.Contains(row))
                return null;
            valueCell = structure.Cells.SingleOrDefault(item =>
                item.Row == row && item.Column == mapping.SourceColumn);
        }
        if (valueCell is null || string.IsNullOrWhiteSpace(valueCell.RawText))
            return null;
        return new InventoryDiscoveredField(
            mapping.CanonicalMeaning,
            mapping.SourceLabel,
            valueCell.RawText,
            valueCell.Locator,
            structure.Id,
            valueCell.PositionJson,
            mapping.Interpretation,
            mapping.Confidence,
            mapping.InterpretedCode,
            []);
    }
}
