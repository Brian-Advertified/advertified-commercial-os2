using System.Text.Json;
using Advertified.Commercial.Application.Planning;

namespace Advertified.Commercial.Infrastructure.Planning;

internal static class PlanningDecisionContext
{
    private static readonly JsonSerializerOptions StoredJson = new(JsonSerializerDefaults.Web);

    internal static PlanningDecisionContextView Build(
        PlanningBriefRow brief,
        AudienceStrategyView? audience,
        MediaMixVersionView? mix)
    {
        var measures = ReadMeasurements(brief.MeasurementJson);
        var jobs = mix?.Allocations
            .Where(item => item.BudgetMinor > 0)
            .Select(item => new PlanningMediaJobView(
                item.Channel, item.Role, item.BudgetMinor, mix.Currency))
            .ToArray() ?? [];
        var gaps = new List<string>();
        if (measures.Length == 0) gaps.Add("commercialFlow.successMeasureMissing");
        if (audience is null) gaps.Add("commercialFlow.audienceStrategyMissing");
        if (jobs.Length == 0) gaps.Add("commercialFlow.mediaJobsMissing");
        if (jobs.Any(item => string.IsNullOrWhiteSpace(item.Role)))
            gaps.Add("commercialFlow.mediaJobRoleMissing");
        return new PlanningDecisionContextView(
            brief.BusinessProblem, brief.Objective, measures,
            audience?.TargetingRationale, audience?.PositioningStatement,
            jobs, gaps);
    }

    private static string[] ReadMeasurements(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(json, StoredJson) ?? [];
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Stored Brief measurement JSON is invalid.");
        }
    }
}
