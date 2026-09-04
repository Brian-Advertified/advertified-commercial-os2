using System.Text.RegularExpressions;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class DoclingInventoryProjection
{
    private static readonly (string Token, string Header)[]
        EmbeddedHeaderTokens =
        [
            ("productcode", "Product Code"),
            ("platform", "Platform"),
            ("adunit", "Ad Unit"),
            ("advertisingunit", "Ad Unit"),
            ("width", "Width"),
            ("height", "Height"),
            ("format", "Format"),
            ("rate", "Rate"),
            ("price", "Price"),
            ("supplier", "Supplier"),
        ];

    [GeneratedRegex(
        @"^\s*\d+(?:[.,]\d+)?\s+\d+(?:[.,]\d+)?\s*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex CombinedDimensionPattern();

    private static Dictionary<int, string> RepairOcrHeaders(
        Dictionary<int, string> headers,
        IReadOnlyList<DoclingCell> cells,
        int headerRow,
        IReadOnlyList<InventoryTableRow> dataRows)
    {
        var repaired = headers.ToDictionary(
            item => item.Key,
            item => item.Value);
        foreach (var column in repaired.Keys.ToArray())
        {
            var normalized = InventoryTabularProjection.NormalizeHeader(
                repaired[column]);
            if (InventoryCandidateNormalizer.RecognizesHeader(normalized))
                continue;

            var embedded = cells
                .Where(cell =>
                    cell.Column == column &&
                    cell.Row <= headerRow)
                .OrderBy(cell => cell.Row)
                .Select(cell => EmbeddedHeader(cell.Text))
                .FirstOrDefault(value => value is not null);
            if (embedded is not null)
                repaired[column] = embedded;
        }

        RepairAdjacentRateColumns(repaired, dataRows);
        RepairCombinedDimensions(repaired, dataRows);
        return repaired;
    }

    private static string? EmbeddedHeader(string value)
    {
        var normalized = InventoryTabularProjection.NormalizeHeader(value);
        return EmbeddedHeaderTokens
            .Select(item => new
            {
                item.Header,
                Position = normalized.IndexOf(
                    item.Token,
                    StringComparison.Ordinal),
            })
            .Where(item => item.Position >= 0)
            .OrderBy(item => item.Position)
            .Select(item => item.Header)
            .FirstOrDefault();
    }

    private static InventoryTableRow[] FillDownContextColumns(
        IReadOnlyDictionary<int, string> headers,
        IReadOnlyList<InventoryTableRow> dataRows)
    {
        var columns = headers
            .Where(item =>
                InventoryTabularProjection.NormalizeHeader(item.Value) ==
                "platform")
            .Select(item => item.Key)
            .ToArray();
        if (columns.Length == 0)
            return dataRows.ToArray();

        var previous = new Dictionary<int, string>();
        var result = new List<InventoryTableRow>();
        foreach (var row in dataRows.OrderBy(item => item.SourceRow))
        {
            var cells = row.Cells.ToDictionary(
                item => item.Key,
                item => item.Value);
            foreach (var column in columns)
            {
                if (cells.TryGetValue(column, out var current) &&
                    !string.IsNullOrWhiteSpace(current))
                {
                    previous[column] = current.Trim();
                }
                else if (previous.TryGetValue(column, out var inherited))
                {
                    cells[column] = inherited;
                }
            }
            result.Add(new InventoryTableRow(row.SourceRow, cells));
        }
        return result.ToArray();
    }

    private static void RepairAdjacentRateColumns(
        Dictionary<int, string> headers,
        IReadOnlyList<InventoryTableRow> dataRows)
    {
        var rateHeaders = headers
            .Where(item => IsRateHeader(item.Value))
            .ToArray();
        foreach (var header in rateHeaders)
        {
            var adjacent = header.Key + 1;
            if (headers.ContainsKey(adjacent) ||
                !dataRows.Any(row =>
                    row.Cells.TryGetValue(adjacent, out var value) &&
                    HasRateValue(value)))
            {
                continue;
            }
            headers[adjacent] = header.Value;
        }
    }

    private static bool IsRateHeader(string value) =>
        InventoryTabularProjection.NormalizeHeader(value) is
            "rate" or "rates" or "ratecard" or "price" or "cost" or
            "netrate" or "netrates" or "discountedrate" or "cpm";

    private static bool HasRateValue(string value) =>
        InventoryMoneyParser.TryParse(value, out _, out _);

    private static void RepairCombinedDimensions(
        Dictionary<int, string> headers,
        IReadOnlyList<InventoryTableRow> dataRows)
    {
        var width = Column(headers, "width");
        var height = Column(headers, "height");
        if (!width.HasValue || !height.HasValue)
            return;

        var widthHasData = dataRows.Any(row =>
            row.Cells.TryGetValue(width.Value, out var value) &&
            !string.IsNullOrWhiteSpace(value));
        var heightValues = dataRows
            .Select(row => row.Cells.GetValueOrDefault(height.Value))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToArray();
        if (widthHasData ||
            heightValues.Length == 0 ||
            heightValues.Any(value =>
                value != "169" &&
                !CombinedDimensionPattern().IsMatch(value)))
        {
            return;
        }

        headers.Remove(width.Value);
        headers[height.Value] = "Width x Height";
    }

    private static int? Column(
        IReadOnlyDictionary<int, string> headers,
        string normalizedHeader) =>
        headers
            .Where(item =>
                InventoryTabularProjection.NormalizeHeader(item.Value) ==
                normalizedHeader)
            .Select(item => (int?)item.Key)
            .FirstOrDefault();
}
