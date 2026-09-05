using System.Text.Json;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventoryReviewGrouping
{
    internal static InventoryReviewTaskGroup[] Group(
        IReadOnlyList<PreparedInventoryCandidate> candidates) => candidates
        .Select(candidate => (Candidate: candidate, Cause: Cause(candidate),
            Scope: Scope(candidate.SourceLocator)))
        .GroupBy(item => (item.Cause, item.Scope))
        .OrderBy(group => group.Key.Scope, StringComparer.Ordinal)
        .ThenBy(group => group.Key.Cause, StringComparer.Ordinal)
        .Select(group => Create(group.Key.Cause, group.Key.Scope,
            group.Select(item => item.Candidate).ToArray()))
        .ToArray();

    private static InventoryReviewTaskGroup Create(
        string cause,
        string scope,
        PreparedInventoryCandidate[] candidates)
    {
        var fields = candidates.SelectMany(candidate => candidate.Validation)
            .Where(issue => issue.IsBlocking)
            .Select(issue => issue.FieldName)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var action = JsonSerializer.Serialize(new
        {
            protocolVersion = "inventory-grouped-review/1.0",
            cause,
            scope,
            affectedCandidateCount = candidates.Length,
            blockingFields = fields,
            correctionAction = "Correct the section schema or shared mapping and reproject the import.",
        }, InventoryRowMapper.StoredJson);
        return new InventoryReviewTaskGroup(Guid.NewGuid(), cause, scope,
            candidates.Length, action);
    }

    private static string Cause(PreparedInventoryCandidate candidate)
    {
        var fields = candidate.Validation.Where(issue => issue.IsBlocking)
            .Select(issue => issue.FieldName).ToArray();
        if (fields.Any(field => field.StartsWith("acceptance.interpretation",
                StringComparison.OrdinalIgnoreCase)))
            return "DOCUMENT_SCHEMA_PROBLEM";
        if (fields.Any(field => field.Contains("package", StringComparison.OrdinalIgnoreCase)))
            return "PACKAGE_RELATIONSHIP_PROBLEM";
        if (fields.Any(field => field.Contains("rateVariant", StringComparison.OrdinalIgnoreCase)))
            return "REPEATED_AMBIGUOUS_COLUMN";
        if (candidate.Values.Extension?.ContainsKey(
                InventoryDiscoveredCandidateNormalizer.UnresolvedMarker) == true)
            return "SECTION_MAPPING_PROBLEM";
        return "ISOLATED_ROW_OR_VALUE_EXCEPTION";
    }

    private static string Scope(string locator)
    {
        var row = locator.IndexOf(";row=", StringComparison.Ordinal);
        return row < 0 ? locator : locator[..row];
    }
}

internal sealed record InventoryReviewTaskGroup(
    Guid Id,
    string Cause,
    string Scope,
    int AffectedCandidateCount,
    string ActionSchemaJson);
