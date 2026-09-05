using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class InventorySchemaBatchProjection
{
    private static bool ProjectMatrixStructure(
        InventorySourceStructure structure,
        InventoryRecordSchema schema,
        IReadOnlyList<string> schemaWarnings,
        List<InventoryExtractedRow> output)
    {
        var boundary = schema.RecordBoundary;
        if (boundary.RowsPerRecord != 1 || boundary.FirstRow < 1)
            return false;
        var byLocator = structure.Cells.ToDictionary(
            cell => cell.Locator, StringComparer.Ordinal);
        var firstHeaderRow = HeaderStart(schema, byLocator, boundary.FirstRow);
        var sourceRows = structure.Cells
            .Where(cell => cell.Row >= firstHeaderRow && cell.Row <= boundary.LastRow)
            .GroupBy(cell => cell.Row)
            .OrderBy(group => group.Key)
            .Select(group => new InventoryTableRow(
                group.Key,
                group.ToDictionary(cell => cell.Column, cell => cell.RawText),
                group.ToDictionary(cell => cell.Column, cell => cell.Locator)))
            .ToArray();
        var cells = structure.Cells.ToDictionary(
            cell => (cell.Row, cell.Column));
        var firstByRow = structure.Cells.GroupBy(cell => cell.Row)
            .ToDictionary(group => group.Key,
                group => group.OrderBy(cell => cell.Column).First().Locator);
        var matrixRows = InventoryHierarchicalMatrixProjection.Project(
            sourceRows, boundary.FirstRow - 1, output.Count,
            row => firstByRow.GetValueOrDefault(row) ??
                structure.Id + ";row=" + row,
            (row, column) => cells.GetValueOrDefault((row, column))?.Locator ??
                structure.Id + ";row=" + row + ";cell=" + column,
            position: (row, column) =>
                cells.GetValueOrDefault((row, column))?.PositionJson);
        if (matrixRows.Length == 0)
            return false;
        foreach (var matrixRow in matrixRows)
            output.Add(AttachDiscoveredFields(matrixRow, structure,
                schema, schemaWarnings, cells, byLocator));
        return true;
    }

    private static int HeaderStart(
        InventoryRecordSchema schema,
        IReadOnlyDictionary<string, InventorySourceCell> cells,
        int firstDataRow)
    {
        var rows = AllMappings(schema)
            .Where(mapping => !mapping.IsDocumentMetadata)
            .Select(mapping => cells.GetValueOrDefault(mapping.SourceLocation)?.Row)
            .Where(row => row.HasValue && row.Value < firstDataRow)
            .Select(row => row!.Value)
            .ToArray();
        return rows.Length == 0 ? firstDataRow - 1 : rows.Min();
    }

    private static InventoryExtractedRow AttachDiscoveredFields(
        InventoryExtractedRow matrixRow,
        InventorySourceStructure structure,
        InventoryRecordSchema schema,
        IReadOnlyList<string> schemaWarnings,
        IReadOnlyDictionary<(int Row, int Column), InventorySourceCell> cells,
        Dictionary<string, InventorySourceCell> byLocator)
    {
        var sourceRow = byLocator[matrixRow.Locator].Row;
        var fields = new List<InventoryDiscoveredField>();
        var uncertain = AllMappings(schema)
            .Where(mapping => !UniformClassification(
                mapping, structure, schema.RecordBoundary))
            .Select(mapping => mapping.SourceLocation)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var mapping in AllMappings(schema))
        {
            if (mapping.CanonicalMeaning is "rate" or "rate_minor")
                continue;
            var source = mapping.IsDocumentMetadata
                ? byLocator.GetValueOrDefault(
                    mapping.ValueSourceLocation ?? mapping.SourceLocation)
                : cells.GetValueOrDefault((sourceRow + mapping.RowOffset,
                    mapping.SourceColumn));
            if (source is not null && !string.IsNullOrWhiteSpace(source.RawText))
            {
                fields.Add(Map(mapping, source,
                    uncertain.Contains(mapping.SourceLocation)));
                continue;
            }
            AddInheritedField(matrixRow, mapping, structure.Id, fields);
        }
        RetainMatrixEvidence(matrixRow, sourceRow, structure, fields);
        return matrixRow with
        {
            DiscoveredFields = fields,
            SchemaWarnings = schemaWarnings.Count == 0 ? null : schemaWarnings,
        };
    }

    private static void AddInheritedField(
        InventoryExtractedRow row,
        InventorySchemaFieldMapping mapping,
        string structureId,
        List<InventoryDiscoveredField> fields)
    {
        if (mapping.IsDocumentMetadata)
            return;
        var key = InventoryTabularProjection.NormalizeHeader(mapping.SourceLabel);
        if (!row.Values.TryGetValue(key, out var value) ||
            string.IsNullOrWhiteSpace(value) ||
            row.FieldLocators?.GetValueOrDefault(key) is not { } locator)
            return;
        fields.Add(new InventoryDiscoveredField(mapping.CanonicalMeaning,
            mapping.SourceLabel, value, locator, structureId, null,
            "Inherited from preceding evidenced source context",
            mapping.Confidence, mapping.InterpretedCode, []));
    }

    private static void RetainMatrixEvidence(
        InventoryExtractedRow row,
        int sourceRow,
        InventorySourceStructure structure,
        List<InventoryDiscoveredField> fields)
    {
        var represented = fields.Select(field => field.SourceLocator)
            .Concat(row.RateVariants?.Select(rate => rate.SourceLocator) ?? [])
            .ToHashSet(StringComparer.Ordinal);
        foreach (var cell in structure.Cells.Where(cell =>
                     cell.Row == sourceRow &&
                     !string.IsNullOrWhiteSpace(cell.RawText) &&
                     !represented.Contains(cell.Locator)))
            fields.Add(new InventoryDiscoveredField(null, string.Empty,
                cell.RawText, cell.Locator, structure.Id, cell.PositionJson,
                "Unmapped source evidence", 0, null,
                ["No section schema mapping was supplied for this source value."]));
    }

    private static InventorySchemaFieldMapping[] AllMappings(
        InventoryRecordSchema schema) => schema.FieldMappings
        .Concat(schema.SupplierMetadataMappings)
        .Concat(schema.AssetMappings)
        .ToArray();
}
