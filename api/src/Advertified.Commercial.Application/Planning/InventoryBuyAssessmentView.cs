namespace Advertified.Commercial.Application.Planning;

public sealed record InventoryBuyAssessmentView(
    long? CampaignSupplierCostMinor,
    string? Currency,
    decimal? Reach,
    decimal? Impressions,
    decimal? AverageFrequency,
    decimal? CostPerThousandImpressionsMinor,
    decimal? CostPerPersonReachedMinor,
    string? Universe,
    string? MeasurementPeriod,
    string? MeasurementSource,
    string? Methodology,
    bool IsTargetAudience,
    InventoryDigitalExposureView? DigitalExposure,
    IReadOnlyList<string> EvidenceGaps);

public sealed record InventoryDigitalExposureView(
    int? SpotLengthSeconds,
    int? SlotLengthSeconds,
    int? LoopLengthSeconds,
    int? PlaysPerLoop,
    decimal? LoopSharePercent);
