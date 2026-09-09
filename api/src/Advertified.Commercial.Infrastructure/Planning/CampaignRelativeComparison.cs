using Advertified.Commercial.Application.Planning;

namespace Advertified.Commercial.Infrastructure.Planning;

internal static class CampaignRelativeComparison
{
    internal static CampaignCombinationView[] Attach(IReadOnlyList<CampaignCombinationView> alternatives,
        IReadOnlyList<InventoryShortlistCandidateView> candidates, MediaMixVersionView mix)
    {
        if (alternatives.Count == 0) return [];
        var baseline = alternatives[0];
        var byId = candidates.ToDictionary(item => item.Id);
        return alternatives.Select(alternative => alternative with
        {
            Comparison = Compare(alternative, baseline, byId, mix),
        }).ToArray();
    }

    private static CampaignRelativeComparisonView Compare(CampaignCombinationView alternative,
        CampaignCombinationView baseline, Dictionary<Guid, InventoryShortlistCandidateView> byId,
        MediaMixVersionView mix)
    {
        var selected = alternative.CandidateIds.Select(id => byId[id]).ToArray();
        var channels = selected.Select(item => item.Channel).ToHashSet(StringComparer.Ordinal);
        return new(alternative.CampaignSupplierCostMinor - baseline.CampaignSupplierCostMinor,
            alternative.CandidateIds.Except(baseline.CandidateIds).Order().ToArray(),
            baseline.CandidateIds.Except(alternative.CandidateIds).Order().ToArray(),
            selected.Count(item => item.Suitability?.BuyAssessment?.IsTargetAudience == true),
            selected.Count(item => item.Suitability?.BuyAssessment is not { Reach: > 0 } and not { Impressions: > 0 }),
            selected.Select(item => item.InventoryTenantId).Distinct().Count(),
            mix.Allocations.Where(item => channels.Contains(item.Channel)).Select(item => item.Role)
                .Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.Ordinal).ToArray());
    }
}
