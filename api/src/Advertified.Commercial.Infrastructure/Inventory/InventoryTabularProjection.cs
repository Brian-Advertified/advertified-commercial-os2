using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal sealed record InventoryTableRow(
    int SourceRow,
    IReadOnlyDictionary<int, string> Cells);

internal static class InventoryTabularProjection
{
    internal static InventoryExtractedRow[] Project(
        IReadOnlyDictionary<int, string> headerCells,
        IEnumerable<InventoryTableRow> dataRows,
        int rowNumberOffset,
        Func<int, string> locator,
        Func<int, int, string>? fieldLocator = null,
        Func<int, int, decimal?>? fieldConfidence = null)
    {
        var headers = headerCells
            .Select(item => (item.Key, Value: NormalizeHeader(item.Value)))
            .Where(item => item.Value.Length > 0)
            .ToDictionary(item => item.Key, item => item.Value);
        return dataRows.OrderBy(row => row.SourceRow)
            .Select(row => new
            {
                Row = row,
                Values = ProjectValues(headers, row.Cells),
            })
            .Where(item => item.Values.Count > 0)
            .Select((item, index) => new InventoryExtractedRow(
                rowNumberOffset + index + 1,
                locator(item.Row.SourceRow),
                item.Values,
                FieldLocators: FieldLocators(
                    headers, item.Row.Cells, item.Row.SourceRow, fieldLocator),
                FieldConfidences: FieldConfidences(
                    headers, item.Row.Cells, item.Row.SourceRow, fieldConfidence)))
            .ToArray();
    }

    internal static string NormalizeHeader(string value) => new(
        value.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static SortedDictionary<string, string> ProjectValues(
        Dictionary<int, string> headers,
        IReadOnlyDictionary<int, string> cells)
    {
        var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var cell in cells.OrderBy(item => item.Key))
        {
            var value = cell.Value.Trim();
            if (headers.TryGetValue(cell.Key, out var header) && value.Length > 0)
            {
                values[header] = value;
            }
        }
        return values;
    }

    private static Dictionary<string, string>? FieldLocators(
        Dictionary<int, string> headers,
        IReadOnlyDictionary<int, string> cells,
        int row,
        Func<int, int, string>? value)
    {
        if (value is null) return null;
        return cells.Where(cell => headers.ContainsKey(cell.Key) &&
                !string.IsNullOrWhiteSpace(cell.Value))
            .GroupBy(cell => headers[cell.Key], StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => value(row, group.Max(cell => cell.Key)),
                StringComparer.Ordinal);
    }

    private static Dictionary<string, decimal?>? FieldConfidences(
        Dictionary<int, string> headers,
        IReadOnlyDictionary<int, string> cells,
        int row,
        Func<int, int, decimal?>? value)
    {
        if (value is null) return null;
        return cells.Where(cell => headers.ContainsKey(cell.Key) &&
                !string.IsNullOrWhiteSpace(cell.Value))
            .GroupBy(cell => headers[cell.Key], StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => value(row, group.Max(cell => cell.Key)),
                StringComparer.Ordinal);
    }
}
