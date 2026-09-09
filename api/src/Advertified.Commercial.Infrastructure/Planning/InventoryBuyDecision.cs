using Advertified.Commercial.Application.Planning;

namespace Advertified.Commercial.Infrastructure.Planning;

internal static class InventoryBuyDecision
{
    internal static InventoryBuyDecisionView Evaluate(
        PreparedShortlistCandidate candidate,
        long? cost,
        bool targetMatch,
        decimal? reach,
        decimal? impressions,
        InventoryDigitalExposureView? digital)
    {
        var supported = new List<string>();
        var blocking = new List<string>();
        if (candidate.Eligibility.IsEligible) supported.Add("buyDecision.eligible");
        else blocking.Add("buyDecision.ineligible");
        if (cost.HasValue) supported.Add("buyDecision.pricedForCampaign");
        else blocking.Add("buyDecision.campaignCostMissing");
        if (ContributesToRequired(candidate.SpatialMatch))
            supported.Add("buyDecision.requiredGeographyContribution");
        else blocking.Add("buyDecision.requiredGeographyMissing");
        if (targetMatch) supported.Add("buyDecision.measuredTargetAudience");
        else blocking.Add("buyDecision.targetAudienceEvidenceMissing");
        if (reach > 0 || impressions > 0) supported.Add("buyDecision.deliveryEvidencePresent");
        else blocking.Add("buyDecision.deliveryEvidenceMissing");
        var creativeMismatch = digital?.SpotLengthSeconds > digital?.SlotLengthSeconds;
        if (creativeMismatch) blocking.Add("buyDecision.creativeDoesNotFit");
        var hardFailure = !candidate.Eligibility.IsEligible || creativeMismatch;
        var code = hardFailure ? "DO_NOT_BUY" : blocking.Count > 0 ? "NEEDS_REVIEW" : "BUY";
        return new InventoryBuyDecisionView(code, supported, blocking);
    }

    private static bool ContributesToRequired(InventorySpatialMatchView spatial) =>
        !spatial.HasRequirements || spatial.RequiredRequirementIds.Count == 0 ||
        spatial.MatchedRequiredRequirementIds.Count > 0;
}
