using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Planning;

internal static class InventoryAudienceMatcher
{
    private const string ProfileGap = "inventory.audienceProfile";
    private const string EvidenceGap = "inventory.audienceProfile.measurementEvidence";
    private const string DeliveryMeasurementsGap =
        "inventory.audienceProfile.deliveryMeasurements";
    private const string DeliveryEvidenceGap =
        "inventory.audienceProfile.deliveryMeasurementEvidence";
    private const string TaxonomyGap = "audience.lsmSem.taxonomy";
    private static readonly JsonSerializerOptions StoredJson =
        new(JsonSerializerDefaults.Web);

    internal static InventoryAudienceFitView Evaluate(
        string? profileJson,
        IReadOnlyList<AudienceDefinitionView> targets)
    {
        var profile = ReadProfile(profileJson);
        var languages = TargetValues(targets.Select(item => item.Language));
        var lifeStages = TargetValues(targets.Select(item => item.LifeStage));
        var lsmSem = TargetValues(targets.Select(item => item.LsmSem));
        var lsmSemMandatory = targets.Any(item => item.LsmSemMandatory);
        if (languages.Length == 0 && lifeStages.Length == 0 && lsmSem.Length == 0)
        {
            return profile is null
                ? new(null, null, null, [], DeliveryMeasurements: [],
                    DeliveryEvidenceGaps: [DeliveryMeasurementsGap],
                    LsmSemMandatory: lsmSemMandatory)
                : Fit(null, null, null, [], profile, lsmSemMandatory);
        }

        if (profile is null)
        {
            return new(null, null, null, [ProfileGap], DeliveryMeasurements: [],
                DeliveryEvidenceGaps: [DeliveryMeasurementsGap],
                LsmSemMandatory: lsmSemMandatory);
        }

        var gaps = new List<string>();
        if (!HasMeasurementEvidence(profile))
        {
            gaps.Add(EvidenceGap);
            return Fit(null, null, null, gaps, profile, lsmSemMandatory);
        }

        var languageScore = Score(languages,
            profile.SpokenLanguages.Concat(profile.UnderstoodLanguages));
        var lifeStageScore = Score(lifeStages, profile.LifeStages);
        decimal? lsmSemScore = null;
        if (lsmSem.Length > 0)
        {
            var taxonomies = targets.Where(item => !string.IsNullOrWhiteSpace(item.LsmSem))
                .Select(item => (item.LsmSemTaxonomy, item.LsmSemTaxonomyVersion)).ToArray();
            if (!HasCompatibleTaxonomy(profile, taxonomies))
            {
                gaps.Add(TaxonomyGap);
            }
            else
            {
                lsmSemScore = Score(lsmSem, profile.LsmSemSegments);
            }
        }
        return Fit(
            languageScore, lifeStageScore, lsmSemScore, gaps, profile,
            lsmSemMandatory);
    }

    internal static EligibilityResult ApplyMandatoryEligibility(
        EligibilityResult eligibility,
        InventoryAudienceFitView fit)
    {
        if (!eligibility.IsEligible || !fit.LsmSemMandatory || fit.LsmSemScore is > 0)
        {
            return eligibility;
        }
        return new(
            false,
            MasterDataCodes.RejectionReasons.MissingInfo,
            "The Brief requires an exact LSM/SEM taxonomy-version and segment match.",
            null);
    }

    private static InventoryAudienceFitView Fit(
        decimal? language,
        decimal? lifeStage,
        decimal? lsmSem,
        IReadOnlyList<string> gaps,
        InventoryAudienceProfileValues profile,
        bool lsmSemMandatory) => new(
            language, lifeStage, lsmSem, gaps,
            profile.MeasurementSource, profile.MeasurementPeriod, profile.Methodology,
            profile.TaxonomyName, profile.TaxonomyVersion,
            DeliveryMeasurements(profile), DeliveryEvidenceGaps(profile), lsmSemMandatory);

    private static InventoryDeliveryMeasurementView[] DeliveryMeasurements(
        InventoryAudienceProfileValues profile) => (profile.Measurements ?? [])
        .Select(item => new InventoryDeliveryMeasurementView(
            item.MetricType, item.Value, item.Unit,
            item.Universe ?? profile.Universe,
            item.MeasurementSource ?? profile.MeasurementSource,
            item.MeasurementPeriod ?? profile.MeasurementPeriod,
            item.Methodology ?? profile.Methodology,
            item.Limitations ?? profile.Limitations))
        .ToArray();

    private static string[] DeliveryEvidenceGaps(
        InventoryAudienceProfileValues profile)
    {
        var measurements = DeliveryMeasurements(profile);
        if (measurements.Length == 0)
        {
            return [DeliveryMeasurementsGap];
        }
        return measurements.Any(item => !item.Value.HasValue ||
            string.IsNullOrWhiteSpace(item.Unit) ||
            string.IsNullOrWhiteSpace(item.MeasurementSource) ||
            string.IsNullOrWhiteSpace(item.MeasurementPeriod) ||
            string.IsNullOrWhiteSpace(item.Methodology))
            ? [DeliveryEvidenceGap]
            : [];
    }

    private static InventoryAudienceProfileValues? ReadProfile(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        try
        {
            var profile = JsonSerializer.Deserialize<InventoryAudienceProfileValues>(
                json, StoredJson);
            return profile?.SpokenLanguages is null ||
                profile.UnderstoodLanguages is null || profile.LifeStages is null ||
                profile.LsmSemSegments is null
                ? null
                : profile;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool HasMeasurementEvidence(InventoryAudienceProfileValues profile) =>
        !string.IsNullOrWhiteSpace(profile.MeasurementSource) &&
        !string.IsNullOrWhiteSpace(profile.MeasurementPeriod) &&
        !string.IsNullOrWhiteSpace(profile.Methodology);

    private static bool HasCompatibleTaxonomy(
        InventoryAudienceProfileValues profile,
        IReadOnlyList<(string? Name, string? Version)> targets) =>
        !string.IsNullOrWhiteSpace(profile.TaxonomyName) &&
        !string.IsNullOrWhiteSpace(profile.TaxonomyVersion) &&
        targets.All(item =>
            Equal(item.Name, profile.TaxonomyName) &&
            Equal(item.Version, profile.TaxonomyVersion));

    private static decimal? Score(
        string[] targets,
        IEnumerable<InventoryAudienceSegmentValue> inventory)
    {
        if (targets.Length == 0)
        {
            return null;
        }
        var supplied = inventory.GroupBy(item => Normalize(item.Label))
            .ToDictionary(group => group.Key, group => group.Max(SegmentScore));
        return targets.Average(target => supplied.GetValueOrDefault(Normalize(target)));
    }

    private static decimal SegmentScore(InventoryAudienceSegmentValue segment) =>
        segment.SharePercent.HasValue ? segment.SharePercent.Value / 100m : 1m;

    private static string[] TargetValues(IEnumerable<string?> values) => values
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .SelectMany(value => value!.Split([';', '|', ','],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    private static bool Equal(string? left, string? right) =>
        left is not null && right is not null && Normalize(left) == Normalize(right);

    private static string Normalize(string value) => string.Concat(value
        .Where(char.IsLetterOrDigit)
        .Select(char.ToLowerInvariant));
}
