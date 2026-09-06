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
        InventoryExtractionResult extraction,
        InventorySemanticOptions settings)
    {
        var items = (extraction.Document.SourceElements ?? [])
            .Where(element =>
                !string.IsNullOrWhiteSpace(element.RawValue))
            .OrderBy(element => element.Locator, StringComparer.Ordinal)
            .SelectMany(element => SplitItem(
                element.Locator,
                SemanticKind(element.StructureKind),
                element.RawValue,
                null,
                settings.MaximumChunkCharacters))
            .ToList();
        if (items.Count == 0)
        {
            items.AddRange(ReadProjectedRows(
                extraction.Rows,
                settings.MaximumChunkCharacters));
        }
        return items;
    }

    private static string SemanticKind(string structureKind) =>
        structureKind.Contains(
            "table",
            StringComparison.OrdinalIgnoreCase)
            ? "TABLE"
            : "TEXT";

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

    private static string[] Sorted(
        IReadOnlySet<string> values) =>
        values.Order(StringComparer.Ordinal).ToArray();

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes(value)));

    private static Guid StepId(string inputHash) =>
        new(Convert.FromHexString(inputHash[..32]));
}
