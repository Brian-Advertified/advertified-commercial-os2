using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

// Deterministic source-content accounting (ADVERTIFIED 11.23): every extracted
// source value must be retained in the projected records, explicitly excluded
// by the record boundary, or referenced by the retained interpretation.
// Well-formed retained JSON alone never proves faithful accounting.
internal static class InventoryAcceptanceSourceAccounting
{
    internal static InventoryAcceptanceCheckEvidence AccountSection(
        InventoryDocumentStructure document,
        InventoryRecordSchema record,
        IReadOnlyList<InventoryExtractedRow> projected)
    {
        var referenced = ReferencedLocators(record);
        foreach (var locator in projected.SelectMany(row =>
                     row.RateVariants?.SelectMany(rate => rate.HeaderLocators) ?? []))
            referenced.Add(locator);
        var retained = projected.SelectMany(row => row.DiscoveredFields ?? [])
            .Select(field => field.SourceLocator).ToHashSet(StringComparer.Ordinal);
        foreach (var locator in projected.SelectMany(row =>
                     row.RateVariants?.Select(rate => rate.SourceLocator) ?? []))
            retained.Add(locator);
        var unaccounted = new List<string>(document.ExtractionGaps ?? []);
        long inBoundary = 0, excluded = 0, context = 0;
        var structure = InventoryRecordOrientation.Apply(
            document.Structures.Single(item => item.Id == record.SourceStructure), record);
        var boundary = record.RecordBoundary;
        var excludedRows = boundary.ExcludedRows.ToHashSet();
        foreach (var cell in structure.Cells)
            AccountCell(cell, boundary, excludedRows, referenced, retained,
                unaccounted, ref inBoundary, ref excluded, ref context);
        var passed = unaccounted.Count == 0;
        return new(InventoryAcceptanceCheck.SourceContentAccounting,
            passed ? InventoryAcceptanceCheckResult.Passed : InventoryAcceptanceCheckResult.Failed,
            record.SourceStructure, passed
                ? $"{inBoundary} in-boundary source values are retained in projected records, {excluded} fall in explicitly excluded record-axis positions, and {context} source positions contain referenced context or empty text."
                : string.Join(" ", unaccounted.Take(3)));
    }

    private static void AccountCell(InventorySourceCell cell, InventoryRecordBoundary boundary,
        HashSet<int> excludedRows, HashSet<string> referenced, HashSet<string> retained,
        List<string> unaccounted, ref long inBoundary, ref long excluded, ref long context)
    {
        // Blank cells remain in structural evidence and cannot produce a commercial value.
        if (string.IsNullOrWhiteSpace(cell.RawText))
        {
            context++;
            return;
        }
        if (cell.Row < boundary.FirstRow || cell.Row > boundary.LastRow)
        {
            context++;
            // Notes, headings and footnotes outside record boundaries must be
            // cited by the interpretation; silent omission is an extraction gap.
            if (!string.IsNullOrWhiteSpace(cell.RawText) && !referenced.Contains(cell.Locator))
                unaccounted.Add(
                    $"Source content outside record boundaries is not accounted for by the interpretation: {cell.Locator}.");
        }
        else if (excludedRows.Contains(cell.Row))
        {
            excluded++;
            if (boundary.ExclusionReasons?.TryGetValue(cell.Row, out var reason) != true ||
                string.IsNullOrWhiteSpace(reason))
                unaccounted.Add($"Excluded source row has no recorded reason: {cell.Locator}.");
        }
        else
        {
            inBoundary++;
            if (!retained.Contains(cell.Locator))
                unaccounted.Add(
                    $"An in-boundary source value was not retained in the projected records: {cell.Locator}.");
        }
    }

    private static HashSet<string> ReferencedLocators(InventoryRecordSchema record)
    {
        var referenced = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mapping in record.FieldMappings.Concat(record.SupplierMetadataMappings)
            .Concat(record.AssetMappings))
        {
            referenced.Add(mapping.SourceLocation);
            if (mapping.ValueSourceLocation is not null)
                referenced.Add(mapping.ValueSourceLocation);
            foreach (var citation in mapping.Evidence)
                referenced.Add(citation.SourceLocator);
        }
        return referenced;
    }
}
