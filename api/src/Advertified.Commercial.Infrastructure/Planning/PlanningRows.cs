using Advertified.Commercial.Application.Planning;

namespace Advertified.Commercial.Infrastructure.Planning;

internal sealed record PlanningBriefRow(
    Guid Id,
    Guid TenantId,
    Guid BriefId,
    Guid OwnerUserId,
    string Status,
    string Objective,
    string AudiencesJson,
    string GeographiesJson,
    long? BudgetMinor,
    bool BudgetUnknown,
    string? Currency,
    string? VatStatus,
    long? FeesMinor,
    string EvidenceIdsJson,
    long Version);

internal sealed record CampaignModeRow(
    Guid Id,
    Guid BriefVersionId,
    string Mode,
    string DecisionSource,
    decimal Confidence,
    string? Reason,
    Guid SelectedBy,
    long Version,
    DateTimeOffset SelectedAtUtc);

internal sealed record AudienceSetRow(
    Guid Id,
    Guid BriefVersionId,
    int VersionNumber,
    string TargetAudienceIdsJson,
    string TargetingRationale,
    string PositioningStatement,
    string InputHash,
    string Status,
    DateTimeOffset CreatedAtUtc);

internal sealed record AudienceDefinitionRow(
    Guid Id,
    string Name,
    string Description,
    string NeedState,
    string BuyingContext,
    string GeographiesJson,
    string? Language,
    string? LifeStage,
    string? LsmSem,
    string Classification,
    string ExclusionsJson,
    string EvidenceIdsJson,
    decimal Confidence,
    string Status);

internal sealed record MediaMixRow(
    Guid Id,
    Guid BriefVersionId,
    Guid AudienceSetId,
    int VersionNumber,
    long TotalBudgetMinor,
    string Currency,
    string AllocationsJson,
    string AssumptionsJson,
    string InputHash,
    string Status,
    Guid CreatedBy,
    Guid? ApprovedBy,
    long Version,
    DateTimeOffset CreatedAtUtc);

internal sealed record ShortlistRow(
    Guid Id,
    Guid BriefVersionId,
    Guid MixVersionId,
    int VersionNumber,
    string InputHash,
    string Status,
    string AssumptionsJson,
    long Version,
    DateTimeOffset CreatedAtUtc);

internal sealed record ShortlistCandidateRow(
    Guid Id,
    Guid InventoryProductId,
    Guid ProductVersionId,
    Guid? RateId,
    Guid? AvailabilityId,
    string Name,
    string Channel,
    string Geography,
    long? RateAmountMinor,
    string? Currency,
    bool IsEligible,
    string? RejectionReason,
    string? RejectionDetail,
    decimal? Score,
    bool? IsSelected,
    Guid? BenchmarkId,
    string? BenchmarkPolicy,
    string? BenchmarkGeography,
    string? BenchmarkStatisticsJson,
    string? BenchmarkPosition,
    decimal? BenchmarkConfidence,
    string? BenchmarkExclusionsJson);

internal sealed record MediaPlanRow(
    Guid Id,
    Guid BriefVersionId,
    Guid MixVersionId,
    Guid ShortlistVersionId,
    int VersionNumber,
    long SubtotalMinor,
    long FeesMinor,
    long VatMinor,
    long TotalMinor,
    string Currency,
    string SupplyConfidence,
    string InputHash,
    string Status,
    string AssumptionsJson,
    string CriticReportJson,
    Guid CreatedBy,
    Guid? ApprovedBy,
    long Version,
    DateTimeOffset CreatedAtUtc);

internal sealed record MediaPlanLineRow(
    Guid PlanVersionId,
    Guid Id,
    Guid InventoryProductId,
    Guid ProductVersionId,
    Guid RateId,
    Guid? AvailabilityId,
    string Name,
    string Channel,
    string Geography,
    DateOnly FlightStart,
    DateOnly FlightEnd,
    string RunningPeriodsJson,
    int Quantity,
    long SupplierCostMinor,
    long ClientPriceMinor,
    long FeesMinor,
    long VatMinor,
    string ForecastJson,
    string Availability,
    string RateFreshness,
    string SupplySource,
    DateTimeOffset? LastConfirmedAtUtc,
    string SupplyConfidence);

internal sealed record ObjectionResolutionRow(
    Guid PlanVersionId,
    string ObjectionCode,
    string Resolution,
    string Reason,
    Guid ResolvedBy);

internal sealed record PlanningInventoryRow(
    Guid ProductId,
    Guid ProductVersionId,
    Guid SupplierId,
    string Name,
    string Channel,
    string ProductType,
    string Geography,
    decimal? Latitude,
    decimal? Longitude,
    Guid? RateId,
    string? RateType,
    string? Currency,
    long? RateAmountMinor,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo,
    Guid? AvailabilityId,
    string? Availability,
    DateTimeOffset? ObservedAtUtc,
    DateTimeOffset? ValidUntilUtc,
    string? AvailabilitySource);

internal sealed record PlanningSpatialPeerRow(
    Guid TargetProductVersionId,
    Guid ProductVersionId,
    decimal DistanceKilometres);

internal sealed record BenchmarkStatistics(
    int CohortSize,
    long? MedianMinor,
    long? LowerQuartileMinor,
    long? UpperQuartileMinor,
    decimal? Percentile);

internal sealed record CriticObjection(
    string Code,
    string Severity,
    string AffectedField,
    string EvidenceGap,
    string RecommendedResolution);

internal sealed record LineForecast(string SupplyConfidence);

internal sealed record EligibilityResult(
    bool IsEligible,
    string? RejectionReason,
    string? RejectionDetail,
    decimal? Score);

internal sealed record BenchmarkResult(
    Guid Id,
    Guid[] ProductVersionIds,
    Guid[] RateIds,
    IReadOnlyDictionary<Guid, decimal> DistancesKilometres,
    string[] Exclusions,
    BenchmarkStatistics Statistics,
    decimal Confidence,
    string Position,
    string GeographyBasis);

internal sealed record ScheduledInventory(
    PlanningInventoryRow Inventory,
    IReadOnlyList<MediaRunningPeriodView> RunningPeriods);

internal sealed record CalculatedLineAmounts(
    PlanningInventoryRow Inventory,
    IReadOnlyList<MediaRunningPeriodView> RunningPeriods,
    int Quantity,
    long SupplierCostMinor,
    long FeesMinor,
    long VatMinor,
    long ClientPriceMinor);

internal sealed record CalculatedPlanAmounts(
    long SubtotalMinor,
    long FeesMinor,
    long VatMinor,
    long TotalMinor,
    IReadOnlyList<CalculatedLineAmounts> Lines);
