using Advertified.Commercial.Application.Planning;

namespace Advertified.Commercial.Infrastructure.Planning;

internal static class CampaignScenarioClassifier
{
    internal static CampaignCombinationView[] Attach(
        IReadOnlyList<CampaignCombinationView> alternatives)
    {
        if (alternatives.Count == 0) return [];
        var baseline = alternatives[0];
        var maxReach = alternatives.Max(item => item.AudienceForecast?.DeduplicatedReach);
        var maxFrequency = alternatives.Max(item => item.AudienceForecast?.AverageFrequency);
        var minCost = alternatives.Min(item => item.CampaignSupplierCostMinor);
        return alternatives.Select((alternative, index) => alternative with
        {
            Scenario = Build(alternative, baseline, index == 0, maxReach, maxFrequency, minCost),
        }).ToArray();
    }

    private static CampaignScenarioView Build(
        CampaignCombinationView value,
        CampaignCombinationView baseline,
        bool recommended,
        decimal? maxReach,
        decimal? maxFrequency,
        long minCost)
    {
        var reachDelta = Delta(
            value.AudienceForecast?.DeduplicatedReach,
            baseline.AudienceForecast?.DeduplicatedReach);
        var frequencyDelta = Delta(
            value.AudienceForecast?.AverageFrequency,
            baseline.AudienceForecast?.AverageFrequency);
        var costDelta = value.CampaignSupplierCostMinor - baseline.CampaignSupplierCostMinor;
        return new CampaignScenarioView(
            Code(value, recommended, reachDelta, frequencyDelta, costDelta,
                maxReach, maxFrequency, minCost),
            recommended, costDelta, reachDelta, frequencyDelta);
    }

    private static string Code(CampaignCombinationView value, bool recommended,
        decimal? reachDelta, decimal? frequencyDelta, long costDelta,
        decimal? maxReach, decimal? maxFrequency, long minCost)
    {
        if (recommended) return "RECOMMENDED";
        if (reachDelta > 0 && maxReach.HasValue &&
            value.AudienceForecast?.DeduplicatedReach == maxReach)
            return "MAX_MEASURED_REACH";
        if (frequencyDelta > 0 && maxFrequency.HasValue &&
            value.AudienceForecast?.AverageFrequency == maxFrequency)
            return "HIGHER_FREQUENCY";
        if (costDelta < 0 && value.CampaignSupplierCostMinor == minCost)
            return "LOWER_SUPPLIER_COST";
        return "ALTERNATIVE";
    }

    private static decimal? Delta(decimal? value, decimal? baseline) =>
        value.HasValue && baseline.HasValue ? value.Value - baseline.Value : null;
}
