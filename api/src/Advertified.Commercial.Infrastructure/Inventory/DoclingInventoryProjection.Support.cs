using System.Text.Json;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class DoclingInventoryProjection
{
    private static TextItem[] ReadTexts(JsonElement root)
    {
        if (!root.TryGetProperty("texts", out var texts) ||
            texts.ValueKind != JsonValueKind.Array)
        {
            return [];
        }
        var result = new List<TextItem>();
        var number = 0;
        foreach (var item in texts.EnumerateArray())
        {
            number++;
            if (!item.TryGetProperty("text", out var value) ||
                value.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(value.GetString()))
            {
                continue;
            }
            result.Add(new TextItem(
                number,
                ReadPage(item),
                value.GetString()!.Trim(),
                ReadConfidence(item)));
        }
        return result.ToArray();
    }

    private static decimal? ReadConfidence(JsonElement value)
    {
        foreach (var name in new[]
                 {
                     "confidence",
                     "ocr_confidence",
                 })
        {
            if (value.TryGetProperty(
                    name, out var confidence) &&
                confidence.TryGetDecimal(out var result) &&
                result is >= 0 and <= 1)
            {
                return result;
            }
        }
        return null;
    }

    private static int ReadPage(JsonElement value)
    {
        if (value.TryGetProperty(
                "prov", out var provenance) &&
            provenance.ValueKind == JsonValueKind.Array &&
            provenance.GetArrayLength() > 0 &&
            provenance[0].TryGetProperty(
                "page_no", out var page) &&
            page.TryGetInt32(out var result))
        {
            return result;
        }
        return 1;
    }

    private static string TableLocator(
        int page,
        int table,
        int row) =>
        "docling:page=" + page +
        ";table=" + table +
        ";row=" + (row + 1);

    private static string CellLocator(
        int page,
        int table,
        int row,
        int column) =>
        TableLocator(page, table, row) +
        ";cell=" + (column + 1);

    private sealed record ProjectionContext(
        IReadOnlyDictionary<string, string> Values,
        IReadOnlyDictionary<string, string> Locators,
        IReadOnlyDictionary<string, string> EvidenceBases,
        IReadOnlyDictionary<string, string> Transformations);

    private sealed record DoclingCell(
        int Row,
        int Column,
        string Text,
        decimal? Confidence);

    private sealed record TextItem(
        int Number,
        int Page,
        string Text,
        decimal? Confidence);

    private sealed class StringTupleComparer :
        IEqualityComparer<(string Page, string Code)>
    {
        internal static readonly StringTupleComparer Instance =
            new();

        public bool Equals(
            (string Page, string Code) left,
            (string Page, string Code) right) =>
            string.Equals(
                left.Page,
                right.Page,
                StringComparison.Ordinal) &&
            string.Equals(
                left.Code,
                right.Code,
                StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(
            (string Page, string Code) value) =>
            HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(value.Page),
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.Code));
    }
}
