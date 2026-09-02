using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal sealed record InventoryCodeSets(
    IReadOnlySet<string> Channels,
    IReadOnlySet<string> ProductTypes,
    IReadOnlySet<string> RateTypes,
    IReadOnlySet<string> Currencies,
    IReadOnlySet<string> Availability,
    IReadOnlySet<string> PerformanceMetrics,
    IReadOnlySet<string> MeasurementUnits,
    IReadOnlySet<string> VatStatuses,
    IReadOnlySet<string> VatTreatments)
{
    internal static async Task<InventoryCodeSets> LoadAsync(
        GovernanceDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var required = new[]
        {
            MasterDataCodes.Channels.Collection, MasterDataCodes.InventoryProductTypes.Collection, MasterDataCodes.RateTypes.Collection, MasterDataCodes.Currencies.Collection,
            MasterDataCodes.AvailabilityStatuses.Collection,
            MasterDataCodes.PerformanceMetricTypes.Collection,
            MasterDataCodes.MeasurementUnits.Collection,
            MasterDataCodes.VatStatuses.Collection,
            MasterDataCodes.VatTreatments.Collection,
        };
        var items = await dbContext.MasterDataItems.AsNoTracking()
            .Where(item => required.Contains(item.CollectionCode) && item.IsActive)
            .Select(item => new { item.CollectionCode, item.Code })
            .ToListAsync(cancellationToken);
        IReadOnlySet<string> Codes(string collection) => items
            .Where(item => item.CollectionCode == collection)
            .Select(item => item.Code).ToHashSet(StringComparer.Ordinal);
        return new(Codes(MasterDataCodes.Channels.Collection), Codes(MasterDataCodes.InventoryProductTypes.Collection), Codes(MasterDataCodes.RateTypes.Collection),
            Codes(MasterDataCodes.Currencies.Collection), Codes(MasterDataCodes.AvailabilityStatuses.Collection),
            Codes(MasterDataCodes.PerformanceMetricTypes.Collection),
            Codes(MasterDataCodes.MeasurementUnits.Collection),
            Codes(MasterDataCodes.VatStatuses.Collection),
            Codes(MasterDataCodes.VatTreatments.Collection));
    }
}

internal static class InventoryCandidateValidator
{
    internal static IReadOnlyList<InventoryValidationIssueView> Validate(
        InventoryCandidateValues values,
        InventoryCodeSets codes)
    {
        var issues = new List<InventoryValidationIssueView>();
        Required(issues, "productCode", values.ProductCode);
        Required(issues, "name", values.Name);
        RequiredCode(issues, "channel", values.Channel, codes.Channels);
        RequiredCode(issues, "productType", values.ProductType, codes.ProductTypes);
        Required(issues, "geography", values.Geography);
        RequiredCode(issues, "rateType", values.RateType, codes.RateTypes);
        RequiredCode(issues, "currency", values.Currency, codes.Currencies);
        if (values.RateAmountMinor is null or < 0)
        {
            issues.Add(Block("rateAmountMinor", MasterDataCodes.ValidationIssueTypes.RateRequired, "A valid non-negative rate is required."));
        }
        RequiredCode(issues, "availability", values.Availability, codes.Availability);
        ValidateCoordinates(values, issues);
        ValidateAudience(values.AudienceProfile, codes, issues);
        ValidateStructured(values, codes, issues);
        return issues;
    }

    private static void ValidateStructured(
        InventoryCandidateValues values,
        InventoryCodeSets codes,
        List<InventoryValidationIssueView> issues)
    {
        var supplier = values.SupplierCommercial;
        var commercial = values.CommercialTerms;
        if (supplier?.VatStatus is null || !codes.VatStatuses.Contains(supplier.VatStatus) ||
            commercial?.VatTreatment is null ||
            !codes.VatTreatments.Contains(commercial.VatTreatment))
        {
            issues.Add(new("supplierCommercial",
                MasterDataCodes.ValidationIssueTypes.SupplierCommercialIncomplete,
                "Supplier VAT status and the rate VAT treatment require review before client pricing.",
                false));
        }
        if (supplier?.VatStatus == MasterDataCodes.VatStatuses.Registered &&
            string.IsNullOrWhiteSpace(supplier.VatNumber))
        {
            issues.Add(new("supplierCommercial.vatNumber",
                MasterDataCodes.ValidationIssueTypes.SupplierCommercialIncomplete,
                "A VAT-registered supplier requires an evidenced VAT number before client pricing.",
                false));
        }
        if ((supplier?.VatStatus == MasterDataCodes.VatStatuses.Registered &&
                commercial?.VatTreatment == MasterDataCodes.VatTreatments.NotApplicable) ||
            (supplier?.VatStatus is MasterDataCodes.VatStatuses.Exempt or
                    MasterDataCodes.VatStatuses.NotApplicable &&
                commercial?.VatTreatment is MasterDataCodes.VatTreatments.Inclusive or
                    MasterDataCodes.VatTreatments.Exclusive))
        {
            issues.Add(Block("commercialTerms.vatTreatment",
                MasterDataCodes.ValidationIssueTypes.CommercialTermsInvalid,
                "Rate VAT treatment is inconsistent with the supplier VAT status."));
        }
        if (commercial is not null && (commercial.RateValidTo < commercial.RateValidFrom ||
            commercial.ProductionCostMinor is < 0 || commercial.InstallationCostMinor is < 0 ||
            commercial.MinimumOrder is <= 0 || commercial.BookingLeadTimeDays is < 0))
        {
            issues.Add(Block("commercialTerms",
                MasterDataCodes.ValidationIssueTypes.CommercialTermsInvalid,
                "Commercial dates, costs, minimum order and lead time must be valid."));
        }
        ValidateDeliverable(values.Deliverable, issues);
        ValidateSpatial(values.Spatial, issues);
    }

    private static void ValidateDeliverable(
        InventoryDeliverableValues? value,
        List<InventoryValidationIssueView> issues)
    {
        if (value is not null && new int?[] { value.SpotLengthSeconds,
                value.LoopLengthSeconds, value.SlotLengthSeconds, value.PlaysPerLoop,
                value.Quantity }.Any(item => item is <= 0))
        {
            issues.Add(Block("deliverable",
                MasterDataCodes.ValidationIssueTypes.CommercialTermsInvalid,
                "Supplied deliverable durations and quantities must be positive."));
        }
    }

    private static void ValidateSpatial(
        InventorySpatialValues? value,
        List<InventoryValidationIssueView> issues)
    {
        if (value is null) return;
        var invalidPoint = value.PointsOfInterest.Any(point =>
            point.Latitude.HasValue != point.Longitude.HasValue ||
            point.Latitude is < -90 or > 90 || point.Longitude is < -180 or > 180);
        var invalidBearing = value.FacingBearingDegrees is < 0 or >= 360;
        var invalidGeometry = !ValidGeoJson(value.CoverageGeoJson, "Polygon", "MultiPolygon") ||
            !ValidGeoJson(value.CatchmentGeoJson, "Polygon", "MultiPolygon") ||
            !ValidGeoJson(value.RouteGeoJson, "LineString", "MultiLineString") ||
            !ValidGeoJson(value.DirectionGeoJson, "LineString");
        if (invalidPoint || invalidBearing || invalidGeometry)
        {
            issues.Add(Block("spatial",
                MasterDataCodes.ValidationIssueTypes.SpatialGeometryInvalid,
                "POI coordinates, bearing and GeoJSON geometry must use valid WGS84 values and supported types."));
        }
    }

    private static bool ValidGeoJson(string? value, params string[] types)
    {
        if (value is null) return true;
        try
        {
            using var document = JsonDocument.Parse(value);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("type", out var type) &&
                types.Contains(type.GetString(), StringComparer.Ordinal) &&
                root.TryGetProperty("coordinates", out var coordinates) &&
                coordinates.ValueKind == JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void ValidateAudience(
        InventoryAudienceProfileValues? profile,
        InventoryCodeSets codes,
        List<InventoryValidationIssueView> issues)
    {
        if (profile is null)
        {
            return;
        }
        var segments = profile.SpokenLanguages.Concat(profile.UnderstoodLanguages)
            .Concat(profile.LifeStages).Concat(profile.LsmSemSegments).ToArray();
        if (segments.Any(item => string.IsNullOrWhiteSpace(item.Label) ||
                item.SharePercent is < 0 or > 100))
        {
            issues.Add(Block("audienceProfile",
                MasterDataCodes.ValidationIssueTypes.AudienceProfileInvalid,
                "Audience labels are required and supplied shares must be between 0 and 100."));
            return;
        }
        var missingMethod = string.IsNullOrWhiteSpace(profile.MeasurementSource) ||
            string.IsNullOrWhiteSpace(profile.MeasurementPeriod) ||
            string.IsNullOrWhiteSpace(profile.Methodology);
        var missingTaxonomy = profile.LsmSemSegments.Count > 0 &&
            (string.IsNullOrWhiteSpace(profile.TaxonomyName) ||
             string.IsNullOrWhiteSpace(profile.TaxonomyVersion));
        if (segments.Length > 0 && (missingMethod || missingTaxonomy))
        {
            issues.Add(new("audienceProfile",
                MasterDataCodes.ValidationIssueTypes.AudienceEvidenceIncomplete,
                "Audience matching requires source, period, methodology and LSM/SEM taxonomy metadata where applicable.",
                false));
        }
        foreach (var measurement in profile.Measurements ?? [])
        {
            if (!codes.PerformanceMetrics.Contains(measurement.MetricType) ||
                !measurement.Value.HasValue || measurement.Value < 0 ||
                string.IsNullOrWhiteSpace(measurement.Unit) ||
                !codes.MeasurementUnits.Contains(measurement.Unit))
            {
                issues.Add(Block("audienceProfile.measurements",
                    MasterDataCodes.ValidationIssueTypes.AudienceProfileInvalid,
                    "Audience measurements require a governed metric, non-negative value and unit."));
                break;
            }
        }
    }

    private static void ValidateCoordinates(
        InventoryCandidateValues values,
        List<InventoryValidationIssueView> issues)
    {
        var paired = values.Latitude.HasValue == values.Longitude.HasValue;
        var range = values.Latitude is null or >= -90 and <= 90 &&
            values.Longitude is null or >= -180 and <= 180;
        if (!paired || !range)
        {
            issues.Add(Block("coordinates", MasterDataCodes.ValidationIssueTypes.CoordinatesInvalid,
                "Latitude and longitude must be supplied together and within valid ranges."));
        }
        if (values.Channel is MasterDataCodes.Channels.Ooh or MasterDataCodes.Channels.Dooh &&
            (!values.Latitude.HasValue || !values.Longitude.HasValue))
        {
            issues.Add(Block("coordinates", MasterDataCodes.ValidationIssueTypes.OohCoordinatesRequired,
                "Out of home inventory requires verified coordinates."));
        }
    }

    private static void Required(
        List<InventoryValidationIssueView> issues,
        string field,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add(Block(field, MasterDataCodes.ValidationIssueTypes.FieldRequired, $"{field} is required before publication."));
        }
    }

    private static void RequiredCode(
        List<InventoryValidationIssueView> issues,
        string field,
        string? value,
        IReadOnlySet<string> allowed)
    {
        if (string.IsNullOrWhiteSpace(value) || !allowed.Contains(value))
        {
            issues.Add(Block(field, MasterDataCodes.ValidationIssueTypes.GovernedCodeRequired,
                $"Select a supported {field} before publication."));
        }
    }

    private static InventoryValidationIssueView Block(
        string field,
        string code,
        string message) => new(field, code, message, true);
}
