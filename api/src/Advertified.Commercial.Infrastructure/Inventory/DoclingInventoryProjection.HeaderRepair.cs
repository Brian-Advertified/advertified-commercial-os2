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
