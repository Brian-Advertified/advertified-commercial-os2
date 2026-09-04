using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class InventorySemanticMerger
{
    private static readonly string[] StableIdentityFields =
        ["productcode", "packagecode"];

    private static readonly string[] NameIdentityQualifiers =
    [
        "address", "latitude", "longitude", "packagecode",
        "programme", "daypart", "venue", "road", "route",
        "placement", "dimensions", "format",
    ];

    private static readonly string[] IdentityConflictFields =
    [
        "productcode", "packagecode", "name", "channel",
        "producttype", "geography", "address", "latitude",
        "longitude", "programme", "daypart", "venue", "road",
        "route", "placement", "dimensions", "spotlengthseconds",
        "buyingunit", "ratetype", "currency", "rate",
        "ratevalidfrom", "ratevalidto", "format",
    ];

    internal static bool SameIdentity(
        InventoryExtractedRow left,
        InventoryExtractedRow right)
    {
        var stableMatch = StableIdentityFields.Any(
            key => SameValue(left, right, key));
        var qualifiedNameMatch =
            SameValue(left, right, "name") &&
            NameIdentityQualifiers.Any(
                key => SameValue(left, right, key));
        return (stableMatch || qualifiedNameMatch) &&
            !IdentityConflictFields.Any(
                key => DifferentSharedValue(left, right, key));
    }

    private static bool SameValue(
        InventoryExtractedRow left,
        InventoryExtractedRow right,
        string key) =>
        left.Values.TryGetValue(key, out var first) &&
        right.Values.TryGetValue(key, out var second) &&
        !string.IsNullOrWhiteSpace(first) &&
        !string.IsNullOrWhiteSpace(second) &&
        string.Equals(
            first.Trim(),
            second.Trim(),
            StringComparison.OrdinalIgnoreCase);

    private static bool DifferentSharedValue(
        InventoryExtractedRow left,
        InventoryExtractedRow right,
        string key) =>
        left.Values.TryGetValue(key, out var first) &&
        right.Values.TryGetValue(key, out var second) &&
        !string.IsNullOrWhiteSpace(first) &&
        !string.IsNullOrWhiteSpace(second) &&
        !string.Equals(
            first.Trim(),
            second.Trim(),
            StringComparison.OrdinalIgnoreCase);
}
