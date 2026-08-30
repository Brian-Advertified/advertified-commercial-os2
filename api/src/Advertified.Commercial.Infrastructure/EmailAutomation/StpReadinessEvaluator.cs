using Advertified.Commercial.Application.Planning;

namespace Advertified.Commercial.Infrastructure.EmailAutomation;

public sealed class StpReadinessEvaluator : IStpReadinessEvaluator
{
    public StpReadinessView Evaluate(
        AudienceDefinitionSetView strategy,
        decimal minimumConfidence)
    {
        if (minimumConfidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumConfidence),
                "STP confidence must be between zero and one.");
        }

        var reasons = new List<string>();
        if (strategy.Definitions.Count == 0)
        {
            reasons.Add("No audience segment was produced.");
        }
        if (strategy.TargetAudienceIds.Count == 0)
        {
            reasons.Add("No target audience was selected.");
        }
        var definitionsById = strategy.Definitions.ToDictionary(item => item.Id);
        if (strategy.TargetAudienceIds.Any(id => !definitionsById.ContainsKey(id)))
        {
            reasons.Add("The target audience selection does not match the segmentation output.");
        }
        if (string.IsNullOrWhiteSpace(strategy.TargetingRationale))
        {
            reasons.Add("The targeting rationale is missing.");
        }
        if (string.IsNullOrWhiteSpace(strategy.PositioningStatement))
        {
            reasons.Add("The positioning direction is missing.");
        }

        var targetDefinitions = strategy.TargetAudienceIds
            .Where(definitionsById.ContainsKey)
            .Select(id => definitionsById[id])
            .ToArray();
        var lowestConfidence = targetDefinitions.Length == 0
            ? 0m
            : targetDefinitions.Min(item => item.Confidence);
        if (lowestConfidence < minimumConfidence)
        {
            reasons.Add("The target audience evidence is below the automatic-send confidence policy.");
        }

        return new StpReadinessView(
            reasons.Count == 0,
            minimumConfidence,
            lowestConfidence,
            reasons);
    }
}
