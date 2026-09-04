using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventoryPendingSupplierValidationPolicy
{
    private static readonly HashSet<string> DeferredPricingFields =
    [
        "rateType",
        "currency",
        "rateAmountMinor",
    ];

    internal static IReadOnlyList<InventoryValidationIssueView> Apply(
        InventoryCandidateValues values,
        IEnumerable<InventoryValidationIssueView> issues)
    {
        if (!IsPendingSupplier(values))
            return issues.ToArray();
        return issues.Where(issue =>
                !DeferredPricingFields.Contains(issue.FieldName))
            .ToArray();
    }

    internal static bool IsPendingSupplier(
        InventoryCandidateValues values) =>
        values.Extension?.TryGetValue(
            "pricingstatus", out var status) == true &&
        string.Equals(
            status,
            InventoryPricingCodes.PendingSupplier,
            StringComparison.Ordinal);
}
