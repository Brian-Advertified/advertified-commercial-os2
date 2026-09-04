using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventoryCandidateAdmissionPolicy
{
    internal static PreparedInventoryCandidate[] Prepare(
        IReadOnlyList<InventoryExtractedRow> rows,
        string sourceHash,
        string selectedSupplier,
        InventoryCodeSets codes,
        DateTimeOffset capturedAtUtc) =>
        rows.Select(row => InventoryCandidateNormalizer.Normalize(
                row, sourceHash, capturedAtUtc))
            .Where(IsSellableCandidate)
            .Select(candidate =>
                InventoryExtractionCompletionPolicy.PrepareCandidate(
                    candidate, selectedSupplier, codes))
            .ToArray();

    internal static bool IsSellableCandidate(
        ExtractedInventoryCandidate candidate)
    {
        var values = candidate.Values;
        if (HasText(values.ProductCode) ||
            HasText(values.Package?.PackageCode) ||
            HasText(values.Package?.PackageName))
        {
            return true;
        }
        if (!HasText(values.Name))
            return false;

        var hasRawRate = candidate.Evidence.Any(item =>
            item.FieldName == "rate" && HasText(item.RawValue));
        var hasSourceAvailability = candidate.Evidence.Any(item =>
            item.FieldName == "availability" &&
            item.RawValue is not null);
        return values.RateAmountMinor.HasValue ||
            hasRawRate ||
            values.Deliverable is not null ||
            HasText(values.Geography) ||
            HasText(values.Address) ||
            values.Spatial is not null ||
            HasText(values.Channel) ||
            HasText(values.ProductType) ||
            hasSourceAvailability;
    }

    private static bool HasText(string? value) =>
        !string.IsNullOrWhiteSpace(value);
}
