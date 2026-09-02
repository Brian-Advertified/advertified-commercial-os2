using Advertified.Commercial.Application.Planning;

namespace Advertified.Commercial.Infrastructure.Planning;

internal static class InventorySuitabilityScorer
{
    internal static PreparedShortlistCandidate[] Score(
        IReadOnlyList<PreparedShortlistCandidate> candidates,
        PlanningPolicy policy)
    {
        var eligible = candidates.Where(item => item.Eligibility.IsEligible).ToArray();
        var portfolioCounts = eligible
            .GroupBy(item => $"{item.Inventory.Channel}|{item.Inventory.Geography}",
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(),
                StringComparer.OrdinalIgnoreCase);
        return candidates.Select(candidate => ScoreCandidate(
            candidate, portfolioCounts, policy)).ToArray();
    }

    internal static InventorySuitabilityView Empty(PlanningPolicy policy) => new(
        policy.SuitabilityPolicyVersion, 0m, 0m, 0m, 0m, 0m, 0m, 0m, []);

    private static PreparedShortlistCandidate ScoreCandidate(
        PreparedShortlistCandidate candidate,
        Dictionary<string, int> portfolioCounts,
        PlanningPolicy policy)
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
        var objectiveFormat = candidate.Allocation is null ? 0m : 1m;
        var budget = BudgetEfficiency(candidate);
        var readiness = InventoryCommercialReadiness.Evaluate(candidate.Inventory);
        var evidence = EvidenceQuality(candidate, readiness);
        var key = $"{candidate.Inventory.Channel}|{candidate.Inventory.Geography}";
        var diversity = portfolioCounts.TryGetValue(key, out var count)
            ? decimal.Divide(1m, count)
            : 0m;
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
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var suitability = new InventorySuitabilityView(
            policy.SuitabilityPolicyVersion,
            Round(geography), Round(audience), Round(objectiveFormat),
            Round(budget), Round(evidence), Round(diversity), Round(total), gaps);
        return candidate with
        {
            Eligibility = candidate.Eligibility with { Score = suitability.Total },
            Suitability = suitability,
        };
    }

    private static decimal BudgetEfficiency(PreparedShortlistCandidate candidate)
    {
        if (candidate.Allocation is null || candidate.Allocation.BudgetMinor <= 0 ||
            !candidate.Inventory.RateAmountMinor.HasValue)
        {
            return 0m;
        }
        var ratio = decimal.Divide(
            candidate.Inventory.RateAmountMinor.Value,
            candidate.Allocation.BudgetMinor);
        return Math.Clamp(1m - ratio, 0m, 1m);
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
