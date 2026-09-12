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
    IReadOnlyList<string> EvidenceGaps,
    InventoryPlannerReasoningView? PlannerReasoning = null,
    InventoryBuyDecisionView? Decision = null);

public sealed record InventoryBuyDecisionView(
    string Code,
    IReadOnlyList<string> SupportedReasons,
    IReadOnlyList<string> BlockingReasons);

public sealed record InventoryPlannerReasoningView(
    string? PlannedChannelRole,
    IReadOnlyList<PlannerAudienceContextView> TargetContexts,
    int RequiredPlacesMatched,
    int RequiredPlacesTotal,
    bool HasMeasuredTargetAudience,
    IReadOnlyList<string> ReviewQuestions,
    IReadOnlyList<string> SupportedReasons,
    IReadOnlyList<string> BuyingWarnings);

public sealed record PlannerAudienceContextView(string Name, string? NeedState, string? BuyingContext);

public sealed record InventoryDigitalExposureView(
    int? SpotLengthSeconds,
    int? SlotLengthSeconds,
    int? LoopLengthSeconds,
    int? PlaysPerLoop,
    decimal? LoopSharePercent);
