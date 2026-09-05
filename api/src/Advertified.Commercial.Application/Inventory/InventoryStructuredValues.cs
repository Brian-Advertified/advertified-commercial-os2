using System.Text.Json.Serialization;

namespace Advertified.Commercial.Application.Inventory;

public sealed record InventorySupplierCommercialValues(
    string? VatStatus,
    string? VatNumber,
    string? CommissionTerms,
    string? PaymentTerms,
    string? CancellationTerms,
    string? BookingDeadlineTerms);

public sealed record InventorySupplierContactValue(
    string? Name,
    string? Role,
    string? Region,
    string? Email,
    string? Phone,
    string? Website,
    string? SocialHandle);

public sealed record InventoryCommercialTermsValues(
    string? VatTreatment,
    DateOnly? RateValidFrom,
    DateOnly? RateValidTo,
    long? ProductionCostMinor,
    long? InstallationCostMinor,
    int? MinimumOrder,
    string? DiscountTerms,
    IReadOnlyList<string> Inclusions,
    IReadOnlyList<string> Exclusions,
    IReadOnlyList<string> Conditions,
    int? BookingLeadTimeDays,
    DateOnly? BookingDeadline,
    DateOnly? MaterialDeadline,
    string? CancellationTerms,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? BillingDays = null);

public sealed record InventoryDeliverableValues(
    string? Format,
    string? BuyingUnit,
    string? Dimensions,
    string? Placement,
    string? Programme,
    string? Daypart,
    int? SpotLengthSeconds,
    int? LoopLengthSeconds,
    int? SlotLengthSeconds,
    int? PlaysPerLoop,
    int? Quantity,
    string? CreativeSpecification);

public sealed record InventoryPointOfInterestValue(
    string Name,
    string? Category,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    decimal? Latitude,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    decimal? Longitude);

public sealed record InventorySpatialValues(
    string? Country,
    string? Province,
    string? Municipality,
    string? Locality,
    string? Venue,
    string? Road,
    string? Route,
    string? TrafficDirection,
    decimal? FacingBearingDegrees,
    IReadOnlyList<InventoryPointOfInterestValue> PointsOfInterest,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? CoverageGeoJson,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? CatchmentGeoJson,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? RouteGeoJson,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? DirectionGeoJson);

public sealed record InventoryPackageValues(
    string? PackageCode,
    string? PackageName,
    IReadOnlyList<string> ComponentProductCodes,
    string? DiscountRule,
    IReadOnlyList<string> Conditions);

public sealed record InventoryRateVariantValues(
    string? RateType,
    long? AmountMinor,
    string? Currency,
    string? BuyingUnit,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    string? Geography,
    string? Daypart,
    string? Days,
    int? DurationSeconds,
    string SourceLocator,
    string RawValue,
    string HeaderHierarchy,
    IReadOnlyList<string> HeaderLocators,
    IReadOnlyDictionary<string, string>? Dimensions);

public sealed record InventoryProductVariantValues(
    string VariantKey,
    string? Name,
    string? Geography,
    IReadOnlyDictionary<string, string> Dimensions,
    IReadOnlyList<string> RateSourceLocators);

public sealed record InventoryPackageComponentValues(
    string? ProductCode,
    string Name,
    decimal? Quantity,
    string? Inclusion,
    string SourceLocator);

public sealed record InventoryDiscountValues(
    string? Kind,
    decimal? Percentage,
    long? AmountMinor,
    string? Currency,
    string? Conditions,
    string SourceLocator);

public sealed record InventorySharedTermsValues(
    string? BuyingUnit,
    string? Currency,
    string? VatTreatment,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    IReadOnlyList<string> Conditions,
    string SourceLocator);
