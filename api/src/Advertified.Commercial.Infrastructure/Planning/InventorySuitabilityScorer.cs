using Advertified.Commercial.Application.Planning;

namespace Advertified.Commercial.Infrastructure.Planning;

internal static class InventorySuitabilityScorer
{
    internal static PreparedShortlistCandidate[] Score(
        IReadOnlyList<PreparedShortlistCandidate> candidates,
        PlanningPolicy policy,
        IReadOnlyList<AudienceDefinitionView>? targets = null)
        => candidates.Select(candidate => ScoreCandidate(candidate, policy, targets ?? [])).ToArray();

    internal static InventorySuitabilityView Empty(PlanningPolicy policy) => new(
        policy.SuitabilityPolicyVersion, 0m, 0m, 0m, 0m, 0m, 0m, 0m, []);

    private static PreparedShortlistCandidate ScoreCandidate(
        PreparedShortlistCandidate candidate,
        PlanningPolicy policy,
        IReadOnlyList<AudienceDefinitionView> targets)
    {
        if (!candidate.Eligibility.IsEligible)
        {
            return candidate with { Suitability = Empty(policy) };
        }
        var geography = candidate.SpatialMatch.HasRequirements
            ? candidate.SpatialMatch.GeographyScore
            : 1m;
        var audience = Average(
            candidate.AudienceFit.LanguageScore,
            candidate.AudienceFit.LifeStageScore,
            candidate.AudienceFit.LsmSemScore);
        // Eligibility and an allocated channel do not establish creative suitability.
        // A listed unit rate cannot establish cost per target person reached, and
        // catalogue density cannot establish incremental campaign reach. Until those
        // comparisons are evidenced, zero means unscored, identified by the gaps below.
        const decimal objectiveFormat = 0m;
        const decimal budget = 0m;
        var readiness = InventoryCommercialReadiness.Evaluate(candidate.Inventory);
        var evidence = EvidenceQuality(candidate, readiness);
        const decimal diversity = 0m;
        var weights = policy.SuitabilityWeights;
        var total = geography * weights.Geography +
            audience * weights.AudienceContext +
            objectiveFormat * weights.ObjectiveFormat +
            budget * weights.BudgetEfficiency +
            evidence * weights.EvidenceQualityFreshness +
            diversity * weights.PortfolioCoverageDiversity;
        var gaps = candidate.AudienceFit.EvidenceGaps
            .Concat(candidate.SpatialMatch.EvidenceGaps)
            .Concat(readiness.EvidenceGaps)
            .Concat([
                "suitability.objectiveFormatEvidence",
                "suitability.comparableTargetExposureCost",
                "suitability.incrementalReachEvidence",
            ])
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var suitability = new InventorySuitabilityView(
            policy.SuitabilityPolicyVersion,
            Round(geography), Round(audience), Round(objectiveFormat),
            Round(budget), Round(evidence), Round(diversity), Round(total), gaps,
            InventoryBuyAssessment.Evaluate(candidate, policy, targets));
        return candidate with
        {
            Eligibility = candidate.Eligibility with { Score = suitability.Total },
            Suitability = suitability,
        };
    }

    private static decimal EvidenceQuality(
        PreparedShortlistCandidate candidate,
        InventoryCommercialReadinessView readiness)
    {
        const decimal criticalFacts = 5m;
        var supplied = 0m;
        if (candidate.Inventory.RateId.HasValue) supplied++;
        if (!string.IsNullOrWhiteSpace(candidate.Inventory.Currency)) supplied++;
        if (!string.IsNullOrWhiteSpace(candidate.Inventory.ProductType)) supplied++;
        if (!string.IsNullOrWhiteSpace(candidate.Inventory.Geography)) supplied++;
        if (readiness.EvidenceGaps.Count == 0) supplied++;
        return supplied / criticalFacts;
    }

    private static decimal Average(params decimal?[] values)
    {
        var supplied = values.Where(item => item.HasValue)
            .Select(item => item!.Value).ToArray();
        return supplied.Length == 0 ? 0m : supplied.Average();
    }

    private static decimal Round(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero);
}
