using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class DoclingInventoryProjection
{
    private static readonly string[] DigitalPlatformTerms =
    [
        "digital", "display", "video", "banner", "audio",
        "pre-roll", "preroll", "mobile",
    ];

    private static InventoryExtractedRow[] ApplyTableContext(
        IReadOnlyDictionary<int, string> headers,
        InventoryExtractedRow[] rows)
    {
        var normalized = headers.Values
            .Select(InventoryTabularProjection.NormalizeHeader)
            .ToHashSet(StringComparer.Ordinal);
        if (!normalized.Contains("platform") ||
            !normalized.Contains("element") ||
            !normalized.Contains("cpm"))
        {
            return rows;
        }
        return rows.Select(ApplyDigitalPlatformContext).ToArray();
    }

    private static InventoryExtractedRow ApplyDigitalPlatformContext(
        InventoryExtractedRow row)
    {
        var platform = row.Values.GetValueOrDefault("platform") ?? string.Empty;
        var element = row.Values.GetValueOrDefault("element") ?? string.Empty;
        var combined = platform + " " + element;
        if (!ContainsDigitalPlatformTerm(combined))
            return row;

        var values = new SortedDictionary<string, string>(
            row.Values.ToDictionary(
                item => item.Key,
                item => item.Value,
                StringComparer.Ordinal),
            StringComparer.Ordinal)
        {
            ["channel"] = MasterDataCodes.Channels.Digital,
            ["ratetype"] = MasterDataCodes.RateTypes.Cpm,
        };
        var cpmLocator = row.FieldLocators?.GetValueOrDefault("cpm")
            ?? row.Locator;
        var locators = Copy(row.FieldLocators);
        locators["channel"] = cpmLocator;
        locators["ratetype"] = cpmLocator;
        var bases = Copy(row.FieldEvidenceBases);
        bases["channel"] = MasterDataCodes.InventoryEvidenceBases.DerivedPolicy;
        bases["ratetype"] = MasterDataCodes.InventoryEvidenceBases.DerivedPolicy;
        var transformations = Copy(row.FieldTransformations);
        transformations["channel"] = MasterDataCodes
            .InventoryTransformationTypes.DerivedFromSourceContext;
        transformations["ratetype"] = MasterDataCodes
            .InventoryTransformationTypes.DerivedFromSourceContext;
        return row with
        {
            Values = values,
            FieldLocators = locators,
            FieldEvidenceBases = bases,
            FieldTransformations = transformations,
        };
    }

    private static bool ContainsDigitalPlatformTerm(string value) =>
        DigitalPlatformTerms.Any(term => value.Contains(
            term,
            StringComparison.OrdinalIgnoreCase));

    private static Dictionary<string, string> Copy(
        IReadOnlyDictionary<string, string>? values) =>
        values?.ToDictionary(
            item => item.Key,
            item => item.Value,
            StringComparer.Ordinal)
        ?? new Dictionary<string, string>(StringComparer.Ordinal);
}
