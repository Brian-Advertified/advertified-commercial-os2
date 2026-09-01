namespace Advertified.Commercial.Application.Planning;

public sealed record AudienceDefinitionView(
    Guid Id,
    string Name,
    string Description,
    string NeedState,
    string BuyingContext,
    IReadOnlyList<string> Geographies,
    string? Language,
    string? LifeStage,
    string? LsmSem,
    string Classification,
    IReadOnlyList<string> Exclusions,
    IReadOnlyList<Guid> EvidenceItemIds,
    decimal Confidence,
    string Status);

public sealed record AudienceDefinitionSetView(
    Guid Id,
    Guid BriefVersionId,
    int VersionNumber,
    IReadOnlyList<Guid> TargetAudienceIds,
    string TargetingRationale,
    string PositioningStatement,
    string InputHash,
    string Status,
    IReadOnlyList<AudienceDefinitionView> Definitions,
    DateTimeOffset CreatedAtUtc);

public sealed record MediaRunningPeriodView(
    DateOnly Start,
    DateOnly End);

public sealed record MediaAllocationView(
    string Channel,
    long BudgetMinor,
    string Role,
    IReadOnlyList<MediaRunningPeriodView> RunningPeriods);

public sealed record MediaMixVersionView(
    Guid Id,
    Guid BriefVersionId,
    Guid AudienceSetId,
    int VersionNumber,
    long TotalBudgetMinor,
    string Currency,
    IReadOnlyList<MediaAllocationView> Allocations,
    IReadOnlyList<string> Assumptions,
    string InputHash,
    string Status,
    Guid CreatedBy,
    Guid? ApprovedBy,
    long Version,
    DateTimeOffset CreatedAtUtc);

public sealed record InventoryBenchmarkView(
    Guid Id,
    string PolicyVersion,
    string GeographyBasis,
    int CohortSize,
    long? MedianMinor,
    long? LowerQuartileMinor,
    long? UpperQuartileMinor,
    decimal? Percentile,
    string Position,
    decimal Confidence,
    IReadOnlyList<string> Exclusions);

public sealed record InventoryShortlistCandidateView(
    Guid Id,
    Guid InventoryTenantId,
    Guid? MarketplaceListingVersionId,
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
    string? Rationale,
    bool? IsSelected,
    InventoryBenchmarkView? Benchmark);

public sealed record InventoryShortlistVersionView(
    Guid Id,
    Guid BriefVersionId,
    Guid MixVersionId,
    int VersionNumber,
    string InputHash,
    string Status,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<InventoryShortlistCandidateView> Candidates,
    long Version,
    DateTimeOffset CreatedAtUtc);

public sealed record PlanObjectionView(
    string Code,
    string Severity,
    string AffectedField,
    string EvidenceGap,
    string RecommendedResolution,
    string? Resolution,
    string? ResolutionReason,
    Guid? ResolvedBy);

public sealed record MediaPlanLineView(
    Guid Id,
    Guid InventoryTenantId,
    Guid? MarketplaceListingVersionId,
    Guid InventoryProductId,
    Guid ProductVersionId,
    Guid RateId,
    Guid? AvailabilityId,
    string Name,
    string Channel,
    string Geography,
    IReadOnlyList<MediaRunningPeriodView> RunningPeriods,
    int Quantity,
    long ClientPriceMinor,
    long FeesMinor,
    long VatMinor,
    string Availability,
    string RateFreshness,
    string SupplySource,
    DateTimeOffset? LastConfirmedAtUtc,
    string SupplyConfidence);

public sealed record MediaPlanVersionView(
    Guid Id,
    Guid BriefVersionId,
    Guid MixVersionId,
    Guid ShortlistVersionId,
    int VersionNumber,
    long FeesMinor,
    long VatMinor,
    long TotalMinor,
    string Currency,
    string SupplyConfidence,
    string InputHash,
    string Status,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<MediaPlanLineView> Lines,
    IReadOnlyList<PlanObjectionView> Objections,
    Guid CreatedBy,
    Guid? ApprovedBy,
    long Version,
    DateTimeOffset CreatedAtUtc);

public sealed record PlanningWorkspaceView(
    Guid BriefId,
    Guid BriefVersionId,
    string ClientName,
    CampaignModeSelectionView? CampaignMode,
    AudienceDefinitionSetView? Audience,
    MediaMixVersionView? MediaMix,
    InventoryShortlistVersionView? Shortlist,
    MediaPlanVersionView? MediaPlan);
