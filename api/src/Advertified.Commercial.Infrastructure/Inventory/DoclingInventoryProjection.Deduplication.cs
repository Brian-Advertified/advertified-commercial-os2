using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class DoclingInventoryProjection
{
    private static bool HasSellableRows(
        InventoryExtractionRequest request,
        IEnumerable<InventoryExtractedRow> rows) =>
        rows.Any(row =>
            InventoryCandidateAdmissionPolicy.IsSellableCandidate(
                InventoryCandidateNormalizer.Normalize(
                    row,
                    request.SourceHash,
                    DateTimeOffset.UnixEpoch)));

    private static InventoryExtractedRow[] DeduplicateRows(
        IReadOnlyList<InventoryExtractedRow> rows,
        out InventoryDeduplicationDecision[] decisions)
    {
        var retained = new List<InventoryExtractedRow>();
        var consolidated = new List<InventoryDeduplicationDecision>();
        foreach (var row in rows)
        {
            var duplicate = retained.FindIndex(existing =>
                SharesExactEvidence(existing, row) &&
                SameOrSubset(existing.Values, row.Values) &&
                SameRateVariants(existing.RateVariants, row.RateVariants));
            if (duplicate < 0)
            {
                retained.Add(row);
                continue;
            }
            var existing = retained[duplicate];
            if (row.Values.Count > existing.Values.Count)
            {
                retained[duplicate] = row;
                consolidated.Add(Decision(row, existing));
            }
            else
            {
                consolidated.Add(Decision(existing, row));
            }
        }
        decisions = consolidated.ToArray();
        return retained.Select((row, index) =>
            row with { Number = index + 1 }).ToArray();
    }

    private static InventoryDeduplicationDecision Decision(
        InventoryExtractedRow retained,
        InventoryExtractedRow consolidated) => new(
        retained.Locator,
        consolidated.Locator,
        "EXACT_EVIDENCE_AND_IDENTICAL_OR_SUBSET_VALUES",
        EvidenceLocators(consolidated).Order(StringComparer.Ordinal).ToArray());

    private static bool SameOrSubset(
        IReadOnlyDictionary<string, string> left,
        IReadOnlyDictionary<string, string> right) =>
        IsSubset(left, right) || IsSubset(right, left);

    private static bool IsSubset(
        IReadOnlyDictionary<string, string> subset,
        IReadOnlyDictionary<string, string> superset) =>
        subset.All(item =>
            superset.TryGetValue(item.Key, out var value) &&
            string.Equals(
                item.Value,
                value,
                StringComparison.OrdinalIgnoreCase));

    private static bool SharesExactEvidence(
        InventoryExtractedRow left,
        InventoryExtractedRow right)
    {
        var leftEvidence = EvidenceLocators(left);
        return EvidenceLocators(right).Any(leftEvidence.Contains);
    }

    private static HashSet<string> EvidenceLocators(
        InventoryExtractedRow row)
    {
        var locators = new HashSet<string>(StringComparer.Ordinal)
        {
            row.Locator,
        };
        foreach (var locator in row.FieldLocators?.Values ?? [])
            locators.Add(locator);
        foreach (var rate in row.RateVariants ?? [])
            locators.Add(rate.SourceLocator);
        return locators;
    }

    private static bool SameRateVariants(
        IReadOnlyList<InventoryExtractedRateVariant>? left,
        IReadOnlyList<InventoryExtractedRateVariant>? right)
    {
        if (left is null || left.Count == 0)
            return right is null || right.Count == 0;
        if (right is null || left.Count != right.Count) return false;
        return left.Zip(right).All(pair =>
            pair.First.SourceLocator == pair.Second.SourceLocator &&
            pair.First.RawValue.Equals(
                pair.Second.RawValue,
                StringComparison.OrdinalIgnoreCase) &&
            pair.First.HeaderHierarchy.Equals(
                pair.Second.HeaderHierarchy,
                StringComparison.Ordinal));
    }
}
