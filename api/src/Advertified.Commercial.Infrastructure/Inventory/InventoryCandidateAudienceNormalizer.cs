using System.Globalization;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class InventoryCandidateNormalizer
{
    private static InventoryAudienceProfileValues? AudienceProfile(
        Dictionary<string, string> values)
    {
        var profile = new InventoryAudienceProfileValues(
            Segments(values, "spoken_languages"),
            Segments(values, "understood_languages"),
            Segments(values, "life_stages"),
            Segments(values, "lsm_sem_segments"),
            Text(values, "audience_taxonomy"),
            Text(values, "audience_taxonomy_version"),
            Text(values, "audience_universe"),
            Text(values, "audience_measurement_source"),
            Text(values, "audience_measurement_period"),
            Text(values, "audience_methodology"),
            Text(values, "audience_limitations"),
            Measurements(values));
        return HasAudienceValue(profile)
            ? profile
            : null;
    }

    private static InventoryAudienceSegmentValue[] Segments(
        Dictionary<string, string> values,
        string field) =>
        !values.TryGetValue(field, out var raw)
            ? []
            : raw.Split(
                    [';', '|', ','],
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(ParseSegment)
                .Where(item => item.Label.Length > 0)
                .ToArray();

    private static InventoryAudienceSegmentValue ParseSegment(
        string value)
    {
        var text = value.Trim();
        var separator = Math.Max(
            text.LastIndexOf(':'),
            text.LastIndexOf('='));
        if (separator > 0 &&
            TryPercent(text[(separator + 1)..], out var separated))
        {
            return new(
                text[..separator].Trim(),
                separated);
        }
        var percentStart = text.LastIndexOf(' ');
        return percentStart > 0 &&
               TryPercent(
                   text[(percentStart + 1)..], out var trailing)
            ? new(text[..percentStart].Trim(), trailing)
            : new(text, null);
    }

    private static bool TryPercent(
        string value,
        out decimal result) =>
        decimal.TryParse(
            value.Trim().TrimEnd('%'),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out result) &&
        result is >= 0 and <= 100;

    private static bool HasAudienceValue(
        InventoryAudienceProfileValues profile) =>
        profile.SpokenLanguages.Count > 0 ||
        profile.UnderstoodLanguages.Count > 0 ||
        profile.LifeStages.Count > 0 ||
        profile.LsmSemSegments.Count > 0 ||
        profile.TaxonomyName is not null ||
        profile.TaxonomyVersion is not null ||
        profile.Universe is not null ||
        profile.MeasurementSource is not null ||
        profile.MeasurementPeriod is not null ||
        profile.Methodology is not null ||
        profile.Limitations is not null ||
        profile.Measurements?.Count > 0;

    private static InventoryAudienceMeasurementValue[] Measurements(
        Dictionary<string, string> values)
    {
        var definitions = new[]
        {
            (
                Field: "audience_reach",
                Metric: MasterDataCodes.PerformanceMetricTypes.Reach),
            (
                Field: "audience_listenership",
                Metric: MasterDataCodes.PerformanceMetricTypes.Listenership),
            (
                Field: "audience_footfall",
                Metric: MasterDataCodes.PerformanceMetricTypes.Footfall),
            (
                Field: "audience_impressions",
                Metric: MasterDataCodes.PerformanceMetricTypes.Impressions),
        };
        return definitions
            .Where(item => values.ContainsKey(item.Field))
            .Select(item => Measurement(
                values, item.Field, item.Metric))
            .ToArray();
    }

    private static InventoryAudienceMeasurementValue Measurement(
        Dictionary<string, string> values,
        string field,
        string metric)
    {
        var raw = values[field].Trim();
        var numeric = raw.EndsWith('%')
            ? raw[..^1]
            : raw;
        var parsed = decimal.TryParse(
            numeric,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : (decimal?)null;
        var unit = Code(values, field + "_unit") ??
            (raw.EndsWith('%')
                ? MasterDataCodes.MeasurementUnits.Percent
                : null);
        return new(
            metric,
            parsed,
            unit,
            Text(values, "audience_universe"),
            Text(values, "audience_measurement_source"),
            Text(values, "audience_measurement_period"),
            Text(values, "audience_methodology"),
            Text(values, "audience_limitations"));
    }
}
