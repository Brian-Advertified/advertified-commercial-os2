namespace Advertified.Commercial.Infrastructure.Inventory;

// Acceptance requires a complete, source-bound positive evaluation. Absence of
// blocking errors by itself is not acceptance authority.
internal static class InventoryCandidateReviewPolicy
{
    internal const string AutoCertifiedMarker = "reviewbasis";
    internal const string AutoCertifiedBasis =
        "AUTO_CERTIFIED_NO_BLOCKING_EXCEPTIONS";

    private static readonly IReadOnlyCollection<string> AmbiguityMarkers =
    [
        InventoryDiscoveredCandidateNormalizer.UnresolvedMarker,
        "rateambiguity",
    ];

    internal static bool RequiresReview(PreparedInventoryCandidate candidate)
    {
        if (!candidate.HasDiscoveredSchema || !InventoryAcceptancePolicy.CanAccept(candidate.Values))
        {
            return true;
        }
        if (candidate.Validation.Any(issue => issue.IsBlocking))
        {
            return true;
        }
        var extension = candidate.Values.Extension;
        return extension is not null &&
            AmbiguityMarkers.Any(marker =>
                extension.ContainsKey(marker));
    }

    internal static PreparedInventoryCandidate MarkAutoCertified(
        PreparedInventoryCandidate candidate)
    {
        if (RequiresReview(candidate))
            throw new InvalidOperationException("Positive acceptance evidence is required.");
        var extension = new Dictionary<string, string>(
            candidate.Values.Extension ?? new Dictionary<string, string>(),
            StringComparer.Ordinal);
        extension[AutoCertifiedMarker] = AutoCertifiedBasis;
        return candidate with
        {
            Values = candidate.Values with { Extension = extension },
        };
    }
}
