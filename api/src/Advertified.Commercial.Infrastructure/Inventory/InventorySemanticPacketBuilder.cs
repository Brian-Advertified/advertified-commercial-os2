using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class InventorySemanticPacketBuilder
{
    private static readonly JsonSerializerOptions WireJson =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy =
                JsonNamingPolicy.SnakeCaseLower,
        };

    private static List<InventorySemanticSourceItem> ReadItems(
        InventoryExtractionRequest request,
        string providerJson,
        IReadOnlyList<InventoryExtractedRow> rows,
        InventorySemanticOptions settings)
    {
        using var document = JsonDocument.Parse(providerJson);
        var root = document.RootElement;
        var items = new List<InventorySemanticSourceItem>
        {
            new("source:file-name", "FILE_NAME", request.FileName, null),
        };
        items.AddRange(ReadTableItems(
            root, settings.MaximumChunkCharacters));
        items.AddRange(ReadTextItems(
            root, settings.MaximumChunkCharacters));
        items.AddRange(ReadNativeRows(
            rows, settings.MaximumChunkCharacters));
        items.AddRange(NativeOfficeImageReader.ReadExclusions(
            request, settings));
        return items;
    }

    private static List<InventorySemanticSourceItem>
        ReadTableItems(
            JsonElement root,
            int maximumCharacters)
    {
        if (!root.TryGetProperty("tables", out var tables) ||
            tables.ValueKind != JsonValueKind.Array)
        {
            return [];
        }
        var items = new List<InventorySemanticSourceItem>();
        var tableNumber = 0;
        foreach (var table in tables.EnumerateArray())
        {
            tableNumber++;
            items.AddRange(ReadTable(
                table, tableNumber, maximumCharacters));
        }
        return items;
    }

    private static List<InventorySemanticSourceItem>
        ReadTextItems(
            JsonElement root,
            int maximumCharacters)
    {
        if (!root.TryGetProperty("texts", out var texts) ||
            texts.ValueKind != JsonValueKind.Array)
        {
            return [];
        }
        var items = new List<InventorySemanticSourceItem>();
        var textNumber = 0;
        foreach (var item in texts.EnumerateArray())
        {
            textNumber++;
            items.AddRange(ReadTextItem(
                item, textNumber, maximumCharacters));
        }
        return items;
    }

    private static List<InventorySemanticSourceItem>
        ReadTextItem(
            JsonElement item,
            int textNumber,
            int maximumCharacters)
    {
        if (!item.TryGetProperty("text", out var text) ||
            text.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(text.GetString()))
        {
            return [];
        }
        var result = new List<InventorySemanticSourceItem>();
        var lineNumber = 0;
        foreach (var line in text.GetString()!.Split(
                     ['\r', '\n'],
                     StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries))
        {
            lineNumber++;
            var locator = "docling:page=" + ReadPage(item) +
                ";text=" + textNumber +
                ";line=" + lineNumber;
            result.AddRange(SplitItem(
                locator,
                "TEXT",
                line,
                ReadConfidence(item),
                maximumCharacters));
        }
        return result;
    }

    private static List<InventorySemanticSourceItem> ReadTable(
        JsonElement table,
        int tableNumber,
        int maximumCharacters)
    {
        if (!table.TryGetProperty("data", out var data) ||
            !data.TryGetProperty(
                "table_cells", out var cells) ||
            cells.ValueKind != JsonValueKind.Array)
        {
            return [];
        }
        var page = ReadPage(table);
        var tableConfidence = ReadConfidence(table);
        var values = cells.EnumerateArray()
            .Select(ReadSemanticCell)
            .Where(cell => cell is not null)
            .Select(cell => cell!)
            .ToArray();
        if (values.Length == 0)
            return [];
        var headerRow = SelectSemanticHeaderRow(values);
        var headers = values
            .Where(cell => cell.Row <= headerRow)
            .GroupBy(cell => cell.Column)
            .ToDictionary(
                group => group.Key,
                group => string.Join(" / ", group
                    .OrderBy(cell => cell.Row)
                    .Select(cell => cell.Text)
                    .Where(text =>
                        !string.IsNullOrWhiteSpace(text))));
        var result = new List<InventorySemanticSourceItem>();
        foreach (var cell in values.Where(
                     cell => cell.Row > headerRow))
        {
            var locator = "docling:page=" + page +
                ";table=" + tableNumber +
                ";row=" + (cell.Row + 1) +
                ";cell=" + (cell.Column + 1);
            var content = headers.TryGetValue(
                    cell.Column, out var header)
                ? "header=" + header + "\nvalue=" + cell.Text
                : "value=" + cell.Text;
            result.AddRange(SplitItem(
                locator,
                "TABLE",
                content,
                cell.Confidence ?? tableConfidence,
                maximumCharacters));
        }
        return result;
    }

    private static SemanticCell? ReadSemanticCell(
        JsonElement cell)
    {
        if (!cell.TryGetProperty("text", out var text) ||
            !cell.TryGetProperty(
                "start_row_offset_idx", out var row) ||
            !cell.TryGetProperty(
                "start_col_offset_idx", out var column) ||
            string.IsNullOrWhiteSpace(text.GetString()))
        {
            return null;
        }
        return new SemanticCell(
            row.GetInt32(),
            column.GetInt32(),
            text.GetString()!.Trim(),
            ReadConfidence(cell));
    }

    private static int SelectSemanticHeaderRow(
        IReadOnlyList<SemanticCell> cells) =>
        cells.Select(cell => cell.Row)
            .Distinct()
            .Order()
            .Take(6)
            .Select(row => new
            {
                Row = row,
                Score = cells
                    .Where(cell => cell.Row == row)
                    .Count(cell =>
                        InventoryCandidateNormalizer
                            .RecognizesHeader(
                                InventoryTabularProjection
                                    .NormalizeHeader(cell.Text)) ||
                        cell.Text.Contains(
                            "20", StringComparison.Ordinal)),
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Row)
            .First()
            .Row;

    private static List<InventorySemanticSourceItem> SplitItem(
        string locator,
        string kind,
        string content,
        decimal? confidence,
        int maximumCharacters)
    {
        if (content.Length <= maximumCharacters)
        {
            return [new(locator, kind, content, confidence)];
        }
        var result = new List<InventorySemanticSourceItem>();
        var start = 0;
        var part = 0;
        while (start < content.Length)
        {
            part++;
            var length = Math.Min(
                maximumCharacters, content.Length - start);
            if (start + length < content.Length)
            {
                var newline = content.LastIndexOf(
                    '\n', start + length - 1, length);
                if (newline > start)
                    length = newline - start + 1;
            }
            result.Add(new(
                locator + ";part=" + part,
                kind,
                content.Substring(start, length).Trim(),
                confidence));
            start += length;
        }
        return result;
    }

    private static List<
        IReadOnlyList<InventorySemanticSourceItem>> Pack(
        IReadOnlyList<InventorySemanticSourceItem> items,
        int maximumCharacters)
    {
        var groups = new List<
            IReadOnlyList<InventorySemanticSourceItem>>();
        var current = new List<InventorySemanticSourceItem>();
        var characters = 0;
        foreach (var item in items)
        {
            var size = item.Content.Length +
                item.Locator.Length + item.Kind.Length + 64;
            if (current.Count > 0 &&
                (current.Count >= 100 ||
                 characters + size > maximumCharacters))
            {
                groups.Add(current.ToArray());
                current = [];
                characters = 0;
            }
            current.Add(item);
            characters += size;
        }
        if (current.Count > 0)
            groups.Add(current.ToArray());
        return groups;
    }

    private static decimal? ReadConfidence(
        JsonElement value)
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

    private static string[] Sorted(
        IReadOnlySet<string> values) =>
        values.Order(StringComparer.Ordinal).ToArray();

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes(value)));

    private static Guid StepId(string inputHash) =>
        new(Convert.FromHexString(inputHash[..32]));

    private sealed record SemanticCell(
        int Row,
        int Column,
        string Text,
        decimal? Confidence);
}
