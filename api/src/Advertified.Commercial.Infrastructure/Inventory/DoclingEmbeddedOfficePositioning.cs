using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class DoclingInventoryExtractionAdapter
{
    private static readonly string[] DStvPositioningPhrases =
    [
        "Welcome to DStv on Digital",
        "Live & VOD Options",
        "ACCESS ANYWHERE, ANY DEVICE, ANYTIME",
    ];

    private static EmbeddedPositioningContext? ReadPositioningContext(
        string providerJson,
        string imageLocator)
    {
        using var document = JsonDocument.Parse(providerJson);
        if (!document.RootElement.TryGetProperty(
                "texts", out var texts) ||
            texts.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var visible = texts.EnumerateArray()
            .Select(item => item.TryGetProperty(
                    "text", out var value) &&
                value.ValueKind == JsonValueKind.String
                    ? value.GetString()?.Trim()
                    : null)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();
        var phrases = DStvPositioningPhrases
            .Select(expected => visible.FirstOrDefault(value =>
                string.Equals(
                    value,
                    expected,
                    StringComparison.OrdinalIgnoreCase)))
            .Where(value => value is not null)
            .Select(value => value!)
            .ToArray();
        if (phrases.Length == 0)
            return null;

        return new EmbeddedPositioningContext(
            "DStv",
            string.Join(" | ", phrases),
            imageLocator);
    }

    private static InventoryExtractedRow[] ApplyPositioningContext(
        IReadOnlyList<InventoryExtractedRow> rows,
        IReadOnlyList<EmbeddedPositioningContext> contexts)
    {
        var dstv = contexts.FirstOrDefault(context =>
            context.Brand == "DStv");
        if (dstv is null)
            return rows.ToArray();

        return rows.Select(row =>
            IsDStvRow(row)
                ? AddDescription(row, dstv)
                : row).ToArray();
    }

    private static bool IsDStvRow(
        InventoryExtractedRow row) =>
        row.Values
            .Where(item => item.Key is "platform" or "name")
            .Any(item => item.Value.StartsWith(
                "dstv",
                StringComparison.OrdinalIgnoreCase));

    private static InventoryExtractedRow AddDescription(
        InventoryExtractedRow row,
        EmbeddedPositioningContext context)
    {
        var values = new SortedDictionary<string, string>(
            row.Values.ToDictionary(
                item => item.Key,
                item => item.Value,
                StringComparer.Ordinal),
            StringComparer.Ordinal);
        if (!values.TryAdd("description", context.Description))
            return row;

        var locators = Copy(row.FieldLocators);
        var bases = Copy(row.FieldEvidenceBases);
        var transformations = Copy(row.FieldTransformations);
        locators["description"] = context.SourceLocator;
        bases["description"] =
            MasterDataCodes.InventoryEvidenceBases.SupplierSupplied;
        transformations["description"] =
            MasterDataCodes.InventoryTransformationTypes.Trim;
        return row with
        {
            Values = values,
            FieldLocators = locators,
            FieldEvidenceBases = bases,
            FieldTransformations = transformations,
        };
    }

    private static Dictionary<string, string> Copy(
        IReadOnlyDictionary<string, string>? source) =>
        source?.ToDictionary(
            item => item.Key,
            item => item.Value,
            StringComparer.Ordinal)
        ?? new Dictionary<string, string>(StringComparer.Ordinal);
}

internal sealed record EmbeddedPositioningContext(
    string Brand,
    string Description,
    string SourceLocator);
