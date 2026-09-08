using Advertified.Commercial.Application.Planning;

namespace Advertified.Commercial.Infrastructure.Planning;

public sealed partial class PlanningCommands
{
    private static InventoryStrategyInput BuildInventoryStrategy(
        PlanningBriefRow brief, MediaMixRow mix, AudienceDefinitionSetView audience,
        IReadOnlyList<AudienceDefinitionView> targets,
        IEnumerable<MediaAllocationView> allocations) => new(
            audience.Id, audience.VersionNumber, mix.Id, mix.VersionNumber,
            brief.Objective, audience.TargetingRationale, audience.PositioningStatement,
            targets.Select(item => new InventoryStrategyAudienceInput(
                item.Id, item.Name, item.NeedState, item.BuyingContext,
                item.Geographies, item.Classification, item.Exclusions,
                item.EvidenceItemIds)).ToArray(),
            allocations.Select(item => new InventoryStrategyAllocationInput(
                item.Channel, item.BudgetMinor, item.Role, item.RunningPeriods)).ToArray());

    private InventoryIntelligenceCandidateInput ToInventoryIntelligenceInput(
        PreparedShortlistCandidate candidate)
    {
        var benchmark = candidate.Benchmark;
        return new InventoryIntelligenceCandidateInput(
            candidate.Id,
            candidate.Inventory.ProductVersionId,
            candidate.Inventory.Name,
            candidate.Inventory.Channel,
            candidate.Inventory.Geography,
            candidate.Inventory.RateAmountMinor,
            candidate.Inventory.Currency,
            candidate.Eligibility.IsEligible,
            candidate.Eligibility.RejectionReason,
            candidate.Eligibility.RejectionDetail,
            candidate.Eligibility.Score,
            candidate.AudienceFit,
            candidate.Suitability,
            benchmark is null
                ? null
                : new InventoryBenchmarkInput(
                    planningPolicy.BenchmarkVersion,
                    benchmark.GeographyBasis,
                    benchmark.Statistics.CohortSize,
                    benchmark.Statistics.MedianMinor,
                    benchmark.Statistics.Percentile,
                    benchmark.Position,
                    benchmark.Confidence,
                    benchmark.Exclusions));
    }
}
