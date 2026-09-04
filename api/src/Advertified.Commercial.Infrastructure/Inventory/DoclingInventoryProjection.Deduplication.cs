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
        IReadOnlyList<InventoryExtractedRow> rows)
    {
        var retained = new List<InventoryExtractedRow>();
        foreach (var row in rows)
        {
            var scope = SourceScope(row.Locator);
            var duplicate = retained.FindIndex(existing =>
                SourceScope(existing.Locator) == scope &&
                SameOrSubset(existing.Values, row.Values));
            if (duplicate < 0)
            {
                retained.Add(row);
                continue;
            }
            if (row.Values.Count > retained[duplicate].Values.Count)
                retained[duplicate] = row;
        }
        return retained.Select((row, index) =>
            row with { Number = index + 1 }).ToArray();
    }

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

    private static string SourceScope(string locator)
    {
        var page = locator.IndexOf(
            ";page=", StringComparison.Ordinal);
        if (page >= 0)
        {
            var end = locator.IndexOf(';', page + 1);
            return end < 0
                ? locator
                : locator[..end];
        }
        var separator = locator.IndexOf(';');
        return separator < 0
            ? locator
            : locator[..separator];
    }
}
