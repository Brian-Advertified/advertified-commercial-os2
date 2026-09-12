using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Planning;

internal static class InventoryPlannerReasoning
{
    internal static InventoryPlannerReasoningView Evaluate(PreparedShortlistCandidate candidate,
        IReadOnlyList<AudienceSegmentView> targets, bool measuredTarget,
        InventoryDigitalExposureView? digital)
    {
        var questions = new List<string> { "plannerReasoning.messageAndMoment", "plannerReasoning.incrementalContribution" };
        if (!measuredTarget) questions.Add("plannerReasoning.targetEvidence");
        if (candidate.SpatialMatch.HasRequirements) questions.Add("plannerReasoning.proximityNotAudience");
        if (candidate.Inventory.Channel == MasterDataCodes.Channels.Dooh)
            questions.Add("plannerReasoning.digitalExposureTradeoff");
        else questions.Add("plannerReasoning.formatExposureTradeoff");
        if (targets.Any(item => string.IsNullOrWhiteSpace(item.NeedState) ||
                string.IsNullOrWhiteSpace(item.BuyingContext)))
            questions.Add("plannerReasoning.incompleteAudienceContext");
        var reasons = Supported(candidate, measuredTarget, digital);
        var warnings = digital?.SpotLengthSeconds > digital?.SlotLengthSeconds
            ? new[] { "plannerReasoning.creativeDoesNotFit" } : Array.Empty<string>();
        return new(candidate.Allocation?.Role,
            targets.Select(item => new PlannerAudienceContextView(item.Name, item.NeedState, item.BuyingContext)).ToArray(),
            candidate.SpatialMatch.MatchedRequiredRequirementIds.Count,
            candidate.SpatialMatch.RequiredRequirementIds.Count, measuredTarget, questions, reasons, warnings);
    }

    private static string[] Supported(PreparedShortlistCandidate candidate, bool measuredTarget,
        InventoryDigitalExposureView? digital)
    {
        var reasons = new List<string>();
        if (measuredTarget) reasons.Add("plannerReasoning.measuredTargetBaseline");
        if (candidate.SpatialMatch.RequiredRequirementIds.Count > 0 &&
            candidate.SpatialMatch.RequiredRequirementIds.All(candidate.SpatialMatch.MatchedRequiredRequirementIds.Contains))
            reasons.Add("plannerReasoning.requiredPlacesCovered");
        if (digital?.LoopSharePercent > 0) reasons.Add("plannerReasoning.quantifiedLoopShare");
        return reasons.ToArray();
    }
}
