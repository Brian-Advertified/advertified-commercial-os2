using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class InventoryExtractionSourceAccounting
{
    private static string Terminal(
        SourceElement element,
        IReadOnlyList<InventoryExtractedRow> rows,
        IReadOnlyList<PreparedInventoryCandidate> candidates,
        bool schemaContext,
        bool admitted,
        bool projected)
    {
        if (rows.SelectMany(row => row.RateVariants ?? [])
            .Any(rate => rate.SourceLocator == element.Locator))
            return InventoryExtractionTraceCodes.Rate;
        if (candidates.Any(candidate => PackageReference(
                candidate, element.Locator)))
            return InventoryExtractionTraceCodes.PackageOrComponent;
        if (candidates.Any(candidate => ProductReference(
                candidate, element.Locator)))
            return InventoryExtractionTraceCodes.ProductField;
        if (candidates.Any(candidate => SharedTermReference(
                candidate, element.Locator)))
            return InventoryExtractionTraceCodes.SharedCondition;
        if (schemaContext && element.Signal == "COMMERCIAL_CONTEXT")
            return InventoryExtractionTraceCodes.SharedCondition;
        if (admitted) return InventoryExtractionTraceCodes.ProductField;
        return projected ? InventoryExtractionTraceCodes.Ambiguous :
            InventoryExtractionTraceCodes.Unsupported;
    }

    private static bool PackageReference(
        PreparedInventoryCandidate candidate,
        string locator) => candidate.Evidence.Any(field =>
            field.SourceLocator == locator &&
            (field.FieldName.Contains("package", StringComparison.OrdinalIgnoreCase) ||
             field.FieldName.Contains("component", StringComparison.OrdinalIgnoreCase) ||
             field.FieldName.Contains("discount", StringComparison.OrdinalIgnoreCase))) ||
        (candidate.Values.PackageComponents ?? []).Any(item =>
            item.SourceLocator == locator) ||
        (candidate.Values.Discounts ?? []).Any(item =>
            item.SourceLocator == locator);

    private static bool ProductReference(
        PreparedInventoryCandidate candidate,
        string locator) => candidate.Evidence.Any(field =>
            field.SourceLocator == locator &&
            field.FieldName is "name" or "product_code");

    private static bool SharedTermReference(
        PreparedInventoryCandidate candidate,
        string locator) => candidate.Values.SharedTerms?.SourceLocator == locator;
}
