using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class InventoryCandidateValueNormalization
{
    internal static InventoryCandidateValues Normalize(InventoryCandidateValues values) =>
        values with
        {
            ProductCode = Text(values.ProductCode),
            Name = Text(values.Name),
            Channel = Code(values.Channel),
            ProductType = Code(values.ProductType),
            Geography = Text(values.Geography),
            Address = Text(values.Address),
            RateType = Code(values.RateType),
            Currency = Code(values.Currency),
            Availability = Code(values.Availability),
            Description = Text(values.Description),
            Extension = values.Extension?.ToDictionary(
                pair => Text(pair.Key) ?? throw new ArgumentException(
                    "Extension keys are required."),
                pair => Text(pair.Value) ?? string.Empty,
                StringComparer.Ordinal),
            AudienceProfile = NormalizeAudience(values.AudienceProfile),
            SupplierCommercial = NormalizeSupplier(values.SupplierCommercial),
            SupplierContacts = NormalizeContacts(values.SupplierContacts),
            CommercialTerms = NormalizeCommercial(values.CommercialTerms),
            Deliverable = NormalizeDeliverable(values.Deliverable),
            Spatial = NormalizeSpatial(values.Spatial),
            Package = NormalizePackage(values.Package),
        };

    internal static void EnsureCorrectionLimits(InventoryCandidateValues values)
    {
        Limit(values.ProductCode, 200, nameof(values.ProductCode));
        Limit(values.Name, 500, nameof(values.Name));
        Limit(values.Channel, 100, nameof(values.Channel));
        Limit(values.ProductType, 100, nameof(values.ProductType));
        Limit(values.Geography, 500, nameof(values.Geography));
        Limit(values.Address, 1_000, nameof(values.Address));
        Limit(values.RateType, 100, nameof(values.RateType));
        Limit(values.Currency, 100, nameof(values.Currency));
        Limit(values.Availability, 100, nameof(values.Availability));
        Limit(values.Description, 4_000, nameof(values.Description));
        EnsureAudienceLimits(values.AudienceProfile);
        EnsureStructuredLimits(values);
        if (values.Extension is null)
        {
            return;
        }
        foreach (var pair in values.Extension)
        {
            Limit(pair.Key, 100, nameof(values.Extension));
            Limit(pair.Value, 1_000, nameof(values.Extension));
        }
    }

    private static string? Text(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Code(string? value) =>
        Text(value)?.ToUpperInvariant().Replace(' ', '_');

    private static InventoryAudienceProfileValues? NormalizeAudience(
        InventoryAudienceProfileValues? profile) => profile is null
        ? null
        : profile with
        {
            SpokenLanguages = Segments(profile.SpokenLanguages),
            UnderstoodLanguages = Segments(profile.UnderstoodLanguages),
            LifeStages = Segments(profile.LifeStages),
            LsmSemSegments = Segments(profile.LsmSemSegments),
            TaxonomyName = Text(profile.TaxonomyName),
            TaxonomyVersion = Text(profile.TaxonomyVersion),
            Universe = Text(profile.Universe),
            MeasurementSource = Text(profile.MeasurementSource),
            MeasurementPeriod = Text(profile.MeasurementPeriod),
            Methodology = Text(profile.Methodology),
            Limitations = Text(profile.Limitations),
            Measurements = Measurements(profile.Measurements),
        };

    private static InventoryAudienceMeasurementValue[] Measurements(
        IReadOnlyList<InventoryAudienceMeasurementValue>? values) => (values ?? [])
        .Select(item => item with
        {
            MetricType = Code(item.MetricType) ?? string.Empty,
            Unit = Code(item.Unit),
            Universe = Text(item.Universe),
            MeasurementSource = Text(item.MeasurementSource),
            MeasurementPeriod = Text(item.MeasurementPeriod),
            Methodology = Text(item.Methodology),
            Limitations = Text(item.Limitations),
        })
        .Where(item => item.MetricType.Length > 0)
        .GroupBy(item => item.MetricType, StringComparer.Ordinal)
        .Select(group => group.First()).ToArray();

    private static InventoryAudienceSegmentValue[] Segments(
        IReadOnlyList<InventoryAudienceSegmentValue> values) => values
        .Select(item => item with { Label = Text(item.Label) ?? string.Empty })
        .Where(item => item.Label.Length > 0)
        .GroupBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
        .Select(group => group.First()).ToArray();

    private static void EnsureAudienceLimits(InventoryAudienceProfileValues? profile)
    {
        if (profile is null)
        {
            return;
        }
        foreach (var segment in profile.SpokenLanguages.Concat(profile.UnderstoodLanguages)
                     .Concat(profile.LifeStages).Concat(profile.LsmSemSegments))
        {
            Limit(segment.Label, 200, nameof(profile));
            if (segment.SharePercent is < 0 or > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(profile));
            }
        }
        Limit(profile.TaxonomyName, 200, nameof(profile));
        Limit(profile.TaxonomyVersion, 100, nameof(profile));
        Limit(profile.Universe, 500, nameof(profile));
        Limit(profile.MeasurementSource, 500, nameof(profile));
        Limit(profile.MeasurementPeriod, 200, nameof(profile));
        Limit(profile.Methodology, 2_000, nameof(profile));
        Limit(profile.Limitations, 2_000, nameof(profile));
        foreach (var measurement in profile.Measurements ?? [])
        {
            Limit(measurement.MetricType, 100, nameof(profile));
            Limit(measurement.Unit, 100, nameof(profile));
            Limit(measurement.Universe, 500, nameof(profile));
            Limit(measurement.MeasurementSource, 500, nameof(profile));
            Limit(measurement.MeasurementPeriod, 200, nameof(profile));
            Limit(measurement.Methodology, 2_000, nameof(profile));
            Limit(measurement.Limitations, 2_000, nameof(profile));
        }
    }

    private static void Limit(string? value, int maximum, string field)
    {
        if (value?.Length > maximum)
        {
            throw new ArgumentOutOfRangeException(field);
        }
    }
}
