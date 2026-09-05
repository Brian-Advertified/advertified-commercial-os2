using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventorySchemaBatchProjection
{
    internal static InventoryExtractedRow[] Project(InventoryDocumentStructure document,
        DiscoveredInventorySchema schema, IReadOnlySet<string> meanings,
        IReadOnlyDictionary<string, IReadOnlySet<string>> governedCodes)
    {
        InventorySchemaValidation.Validate(document, schema, meanings, governedCodes);
        var structures = document.Structures.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var result = new List<InventoryExtractedRow>();
        foreach (var record in schema.Records)
            ProjectStructure(structures[record.SourceStructure], record, schema.Warnings, result);
        return result.ToArray();
    }

    private static void ProjectStructure(InventorySourceStructure structure, InventoryRecordSchema schema,
        IReadOnlyList<string> schemaWarnings, List<InventoryExtractedRow> output)
    {
        var byPosition = structure.Cells.ToDictionary(cell => (cell.Row, cell.Column));
        var byLocator = structure.Cells.ToDictionary(cell => cell.Locator, StringComparer.Ordinal);
        var byRow = structure.Cells.GroupBy(cell => cell.Row).ToDictionary(group => group.Key, group => group.ToArray());
        var boundary = schema.RecordBoundary;
        var excluded = boundary.ExcludedRows.ToHashSet();
        var mappings = schema.FieldMappings.Concat(schema.SupplierMetadataMappings).Concat(schema.AssetMappings).ToArray();
        var uncertainClassifications = mappings.Where(mapping => !UniformClassification(mapping, structure, boundary))
            .Select(mapping => mapping.SourceLocation).ToHashSet(StringComparer.Ordinal);
        for (var start = boundary.FirstRow; start <= boundary.LastRow; start += boundary.RowsPerRecord)
        {
            var cells = Enumerable.Range(start, Math.Min(boundary.RowsPerRecord, boundary.LastRow - start + 1))
                .Where(row => !excluded.Contains(row)).SelectMany(row => byRow.GetValueOrDefault(row) ?? []).ToArray();
            if (cells.Length == 0) continue;
            var fields = new List<InventoryDiscoveredField>();
            var warnings = new List<string>(schemaWarnings);
            if (boundary.LastRow - start + 1 < boundary.RowsPerRecord)
                warnings.Add("The final record does not contain the full discovered row span.");
            foreach (var mapping in mappings)
            {
                var source = mapping.IsDocumentMetadata
                    ? byLocator[mapping.ValueSourceLocation ?? mapping.SourceLocation]
                    : byPosition.GetValueOrDefault((start + mapping.RowOffset, mapping.SourceColumn));
                if (source is not null && (mapping.IsDocumentMetadata ||
                    (source.Row <= boundary.LastRow && !excluded.Contains(source.Row))))
                    fields.Add(Map(mapping, source, uncertainClassifications.Contains(mapping.SourceLocation)));
                else if (InventoryAcceptanceCandidateChecks.RequiredMapping(mapping.CanonicalMeaning))
                    warnings.Add($"A required mapped source value is missing: {mapping.SourceLocation}.");
            }
            RetainUnmapped(cells, fields, structure.Id);
            output.Add(new InventoryExtractedRow(output.Count + 1, cells[0].Locator,
                new Dictionary<string, string>(), MasterDataCodes.InventoryExtractionMethods.Tabular,
                DiscoveredFields: fields, SchemaWarnings: warnings.Count == 0 ? null : warnings));
        }
    }

    private static InventoryDiscoveredField Map(InventorySchemaFieldMapping mapping, InventorySourceCell cell,
        bool ambiguousClassification) =>
        new(mapping.CanonicalMeaning, mapping.SourceLabel, cell.RawText, cell.Locator, mapping.SourceStructure,
            cell.PositionJson, mapping.Interpretation, mapping.Confidence, mapping.InterpretedCode,
            ambiguousClassification ? ["A single classification is not supported for varying source values."] :
            mapping.CanonicalMeaning is null ? ["The field meaning remains unresolved."] : []);

    private static bool UniformClassification(InventorySchemaFieldMapping mapping,
        InventorySourceStructure structure, InventoryRecordBoundary boundary) =>
        mapping.InterpretedCode is null || mapping.IsDocumentMetadata || structure.Cells.Where(cell =>
            cell.Column == mapping.SourceColumn && cell.Row >= boundary.FirstRow && cell.Row <= boundary.LastRow &&
            !boundary.ExcludedRows.Contains(cell.Row) &&
            (cell.Row - boundary.FirstRow) % boundary.RowsPerRecord == mapping.RowOffset)
        .Select(cell => cell.RawText.Trim()).Distinct(StringComparer.Ordinal).Take(2).Count() <= 1;

    private static void RetainUnmapped(IEnumerable<InventorySourceCell> cells,
        List<InventoryDiscoveredField> fields, string structureId)
    {
        var mapped = fields.Select(field => field.SourceLocator).ToHashSet(StringComparer.Ordinal);
        foreach (var cell in cells.Where(cell => !mapped.Contains(cell.Locator)))
            fields.Add(new InventoryDiscoveredField(null, string.Empty, cell.RawText, cell.Locator,
                structureId, cell.PositionJson, "Unmapped source evidence", 0, null,
                ["No schema mapping was supplied for this source value."]));
    }
}
