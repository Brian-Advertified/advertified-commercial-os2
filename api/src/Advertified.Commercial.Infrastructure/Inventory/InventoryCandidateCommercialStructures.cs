using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class InventoryCandidateNormalizer
{
    private static readonly string[] SharedTermFields =
    [
        "buying_unit", "currency", "vat_treatment", "rate_valid_from",
        "rate_valid_to", "conditions",
    ];

    private static InventoryCandidateValues ApplyCommercialStructures(
        InventoryCandidateValues values,
        IReadOnlyList<InventoryFieldEvidenceView> evidence)
    {
        var components = PackageComponents(values.Package, evidence);
        var discounts = Discounts(values, evidence);
        var shared = SharedTerms(values, evidence);
        return values with
        {
            PackageComponents = components.Length == 0 ? null : components,
            Discounts = discounts.Length == 0 ? null : discounts,
            SharedTerms = shared,
        };
    }

    private static InventoryPackageComponentValues[] PackageComponents(
        InventoryPackageValues? package,
        IReadOnlyList<InventoryFieldEvidenceView> evidence)
    {
        if (package?.ComponentProductCodes is not { Count: > 0 }) return [];
        var locator = Locator(evidence, "package_component_codes");
        if (locator is null) return [];
        return package.ComponentProductCodes.Select(code =>
            new InventoryPackageComponentValues(code, code, null, null, locator))
            .ToArray();
    }

    private static InventoryDiscountValues[] Discounts(
        InventoryCandidateValues values,
        IReadOnlyList<InventoryFieldEvidenceView> evidence)
    {
        var result = new List<InventoryDiscountValues>();
        AddDiscount(result, values.Package?.DiscountRule,
            Locator(evidence, "package_discount_rule"), values.Currency);
        AddDiscount(result, values.CommercialTerms?.DiscountTerms,
            Locator(evidence, "discount_terms"), values.Currency);
        return result.GroupBy(item => (item.Conditions, item.SourceLocator))
            .Select(group => group.First()).ToArray();
    }

    private static void AddDiscount(
        List<InventoryDiscountValues> result,
        string? conditions,
        string? locator,
        string? currency)
    {
        if (string.IsNullOrWhiteSpace(conditions) || locator is null) return;
        result.Add(new InventoryDiscountValues(null, null, null,
            currency, conditions, locator));
    }

    private static InventorySharedTermsValues? SharedTerms(
        InventoryCandidateValues values,
        IReadOnlyList<InventoryFieldEvidenceView> evidence)
    {
        var locator = SharedTermFields.Select(field => Locator(evidence, field))
            .FirstOrDefault(value => value is not null);
        var commercial = values.CommercialTerms;
        var buyingUnit = values.Deliverable?.BuyingUnit;
        if (locator is null || buyingUnit is null && values.Currency is null &&
            commercial?.VatTreatment is null && commercial?.RateValidFrom is null &&
            commercial?.RateValidTo is null && commercial?.Conditions.Count is not > 0)
            return null;
        return new InventorySharedTermsValues(buyingUnit, values.Currency,
            commercial?.VatTreatment, commercial?.RateValidFrom,
            commercial?.RateValidTo, commercial?.Conditions ?? [], locator);
    }

    private static string? Locator(
        IReadOnlyList<InventoryFieldEvidenceView> evidence,
        string field) => evidence.FirstOrDefault(item =>
            item.FieldName.Equals(field, StringComparison.Ordinal))?.SourceLocator;
}
