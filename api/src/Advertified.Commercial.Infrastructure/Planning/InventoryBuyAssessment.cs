using System.Globalization;
using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Planning;

internal static class InventoryBuyAssessment
{
    private static readonly JsonSerializerOptions StoredJson = new(JsonSerializerDefaults.Web);

    internal static InventoryBuyAssessmentView Evaluate(PreparedShortlistCandidate candidate,
        PlanningPolicy policy, IReadOnlyList<AudienceSegmentView> targets)
    {
        var gaps = new List<string>();
        var cost = Cost(candidate, policy, gaps);
        var measurements = CompatibleMeasurements(candidate, gaps);
        var reach = Metric(measurements, MasterDataCodes.PerformanceMetricTypes.Reach);
        var impressions = Metric(measurements, MasterDataCodes.PerformanceMetricTypes.Impressions);
        var basis = measurements.FirstOrDefault();
        var targetMatch = MatchesTarget(basis, targets);
        if (!targetMatch) gaps.Add("buyAssessment.targetUniverse");
        if (reach is null) gaps.Add("buyAssessment.reach");
        if (impressions is null) gaps.Add("buyAssessment.impressions");
        // Supplied measurements are a comparable baseline, never a booked-buy forecast.
        gaps.Add("buyAssessment.measurementNotForecast");
        var digital = Digital(candidate.Inventory, gaps);
        var decision = InventoryBuyDecision.Evaluate(
            candidate, cost, targetMatch, reach, impressions, digital);
        return new(cost, candidate.Inventory.Currency, reach, impressions,
            Frequency(reach, impressions), CostRatio(cost, impressions, 1000m), CostRatio(cost, reach, 1m),
            basis?.Universe, basis?.MeasurementPeriod, basis?.MeasurementSource, basis?.Methodology,
            targetMatch, digital, gaps.Distinct(StringComparer.Ordinal).ToArray(),
            InventoryPlannerReasoning.Evaluate(candidate, targets, targetMatch, digital), decision);
    }

    private static bool MatchesTarget(InventoryDeliveryMeasurementView? basis,
        IReadOnlyList<AudienceSegmentView> targets) => basis is not null && targets.Count == 1 &&
        targets[0].EvidenceItemIds.Count > 0 && string.Equals(basis.Universe?.Trim(),
            targets[0].Name.Trim(), StringComparison.OrdinalIgnoreCase);

    private static decimal? Frequency(decimal? reach, decimal? impressions) =>
        reach > 0 && impressions >= reach ? Round(impressions.Value / reach.Value) : null;

    private static decimal? CostRatio(long? cost, decimal? denominator, decimal scale) =>
        cost.HasValue && denominator > 0 ? Round(cost.Value * scale / denominator.Value) : null;

    private static long? Cost(PreparedShortlistCandidate candidate, PlanningPolicy policy, List<string> gaps)
    {
        try
        {
            if (candidate.Allocation is not null && candidate.Allocation.RunningPeriods.Count > 0)
                return SupplierRateCalculator.Calculate(candidate.Inventory,
                    candidate.Allocation.RunningPeriods, policy,
                    InventoryPurchaseQuantities.Find(candidate.Inventory, candidate.Allocation)).PayableMinor;
        }
        catch (Exception error) when (error is UnpriceableRateException or OverflowException)
        {
            // A missing purchase quantity or billing period is not a unit-price fallback.
        }
        gaps.Add("buyAssessment.campaignCost");
        return null;
    }

    private static InventoryDeliveryMeasurementView[] CompatibleMeasurements(
        PreparedShortlistCandidate candidate, List<string> gaps)
    {
        var supplied = (candidate.AudienceFit.DeliveryMeasurements ?? [])
            .Where(item => item.MetricType is MasterDataCodes.PerformanceMetricTypes.Reach or
                MasterDataCodes.PerformanceMetricTypes.Impressions).ToArray();
        var valid = supplied.Where(item => HasEvidence(item) &&
            MatchesPeriod(item.MeasurementPeriod!, candidate.Allocation?.RunningPeriods ?? [])).ToArray();
        if (valid.Length != supplied.Length) gaps.Add("buyAssessment.measurementEvidenceOrPeriod");
        if (valid.Any(item => !string.IsNullOrWhiteSpace(item.Limitations)))
            gaps.Add("buyAssessment.sourceLimitations");
        // Never combine different universes, dates, studies or methodologies into frequency.
        if (valid.Select(item => (item.Universe, item.MeasurementPeriod,
                item.MeasurementSource, item.Methodology)).Distinct().Count() > 1 ||
            valid.GroupBy(item => item.MetricType).Any(group => group.Count() > 1))
        {
            gaps.Add("buyAssessment.incompatibleMeasurements");
            return [];
        }
        if (Metric(valid, MasterDataCodes.PerformanceMetricTypes.Reach) >
            Metric(valid, MasterDataCodes.PerformanceMetricTypes.Impressions))
        {
            gaps.Add("buyAssessment.inconsistentReachImpressions");
            return [];
        }
        return valid;
    }

    private static bool HasEvidence(InventoryDeliveryMeasurementView value) =>
        value.Value is > 0 && !string.IsNullOrWhiteSpace(value.Universe) &&
        !string.IsNullOrWhiteSpace(value.MeasurementSource) &&
        !string.IsNullOrWhiteSpace(value.Methodology) &&
        !string.IsNullOrWhiteSpace(value.MeasurementPeriod) &&
        (value.MetricType == MasterDataCodes.PerformanceMetricTypes.Reach
            ? value.Unit == MasterDataCodes.MeasurementUnits.People
            : value.Unit == MasterDataCodes.MeasurementUnits.Count);

    private static bool MatchesPeriod(string text, IReadOnlyList<MediaRunningPeriodView> periods)
    {
        // ISO interval syntax is deliberate: free prose/month names are not an exact buy period.
        var parts = text.Split('/', StringSplitOptions.TrimEntries);
        return periods.Count == 1 && parts.Length == 2 &&
            DateOnly.TryParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var start) &&
            DateOnly.TryParseExact(parts[1], "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var end) &&
            start == periods[0].Start && end == periods[0].End && end >= start;
    }

    private static InventoryDigitalExposureView? Digital(PlanningInventoryRow inventory, List<string> gaps)
    {
        if (inventory.Channel != MasterDataCodes.Channels.Dooh) return null;
        var value = ReadDeliverable(inventory.DeliverableJson, gaps);
        var share = LoopShare(value);
        if (share is null) gaps.Add("buyAssessment.digitalLoop");
        if (value?.SpotLengthSeconds is not > 0) gaps.Add("buyAssessment.creativeDuration");
        if (value?.SpotLengthSeconds > value?.SlotLengthSeconds) gaps.Add("buyAssessment.creativeExceedsSlot");
        // Operating hours and actual contracted schedule do not exist in this input schema.
        gaps.Add("buyAssessment.operatingHoursAndContractedPlays");
        return ExposureView(value, share);
    }

    private static InventoryDigitalExposureView ExposureView(InventoryDeliverableValues? value, decimal? share) =>
        new(value?.SpotLengthSeconds, value?.SlotLengthSeconds, value?.LoopLengthSeconds, value?.PlaysPerLoop, share);

    private static decimal? LoopShare(InventoryDeliverableValues? value)
    {
        if (value?.SlotLengthSeconds is not > 0 || value.LoopLengthSeconds is not > 0 ||
            value.PlaysPerLoop is not > 0) return null;
        var seconds = (decimal)value.SlotLengthSeconds.Value * value.PlaysPerLoop.Value;
        return seconds <= value.LoopLengthSeconds.Value ? Round(100m * seconds / value.LoopLengthSeconds.Value) : null;
    }

    private static InventoryDeliverableValues? ReadDeliverable(string? json, List<string> gaps)
    {
        try
        {
            return json is null ? null : JsonSerializer.Deserialize<InventoryDeliverableValues>(json, StoredJson);
        }
        catch (JsonException)
        {
            gaps.Add("buyAssessment.digitalDeliveryEvidence");
            return null;
        }
    }

    private static decimal? Metric(IEnumerable<InventoryDeliveryMeasurementView> measurements, string code) =>
        measurements.SingleOrDefault(item => item.MetricType == code)?.Value;

    private static decimal Round(decimal value) => decimal.Round(value, 4, MidpointRounding.AwayFromZero);
}
