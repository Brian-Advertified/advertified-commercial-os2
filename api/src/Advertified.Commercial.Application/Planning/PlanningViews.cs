using Advertified.Commercial.Application.Inventory;

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
    string? LsmSemTaxonomy,
    string? LsmSemTaxonomyVersion,
    string Classification,
    IReadOnlyList<string> Exclusions,
    IReadOnlyList<Guid> EvidenceItemIds,
    decimal Confidence,
    string Status,
    bool LsmSemMandatory = false);

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

public sealed record InventoryDeliveryMeasurementView(
    string MetricType,
    decimal? Value,
    string? Unit,
    string? Universe,
    string? MeasurementSource,
    string? MeasurementPeriod,
    string? Methodology,
    string? Limitations);

public sealed record InventoryAudienceFitView(
    decimal? LanguageScore,
    decimal? LifeStageScore,
    decimal? LsmSemScore,
    IReadOnlyList<string> EvidenceGaps,
    string? MeasurementSource = null,
    string? MeasurementPeriod = null,
    string? Methodology = null,
    string? TaxonomyName = null,
    string? TaxonomyVersion = null,
    IReadOnlyList<InventoryDeliveryMeasurementView>? DeliveryMeasurements = null,
    IReadOnlyList<string>? DeliveryEvidenceGaps = null,
    bool LsmSemMandatory = false);

public sealed record InventoryCommercialReadinessView(
    string? SupplierVatStatus,
    string? VatTreatment,
    IReadOnlyList<string> EvidenceGaps,
    string? SupplierVatNumber = null);

public sealed record InventorySpatialMatchView(
    bool HasRequirements,
    IReadOnlyList<Guid> RequiredRequirementIds,
    IReadOnlyList<Guid> MatchedRequiredRequirementIds,
    IReadOnlyList<Guid> PreferredRequirementIds,
    IReadOnlyList<Guid> MatchedPreferredRequirementIds,
    IReadOnlyList<Guid> ExcludedRequirementIds,
    IReadOnlyList<Guid> MatchedExcludedRequirementIds,
    decimal GeographyScore,
    IReadOnlyList<string> EvidenceGaps);

public sealed record InventorySuitabilityView(
    string PolicyVersion,
    decimal Geography,
    decimal AudienceContext,
    decimal ObjectiveFormat,
    decimal BudgetEfficiency,
    decimal EvidenceQualityFreshness,
    decimal PortfolioCoverageDiversity,
    decimal Total,
    IReadOnlyList<string> EvidenceGaps);

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
    InventoryAudienceFitView AudienceFit,
    string? Rationale,
    bool? IsSelected,
    InventoryBenchmarkView? Benchmark,
    Guid? LogoAssetId = null,
    InventoryCommercialReadinessView? CommercialReadiness = null,
    InventorySupplierCommercialValues? SupplierCommercial = null,
    InventoryCommercialTermsValues? CommercialTerms = null,
    InventoryDeliverableValues? Deliverable = null,
    InventorySpatialValues? Spatial = null,
    InventorySpatialMatchView? SpatialMatch = null,
    InventorySuitabilityView? Suitability = null);

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
    string SupplyConfidence,
    InventorySupplierCommercialValues? SupplierCommercial = null,
    InventoryCommercialTermsValues? CommercialTerms = null,
    InventoryDeliverableValues? Deliverable = null,
    InventorySpatialValues? Spatial = null,
    Guid? LogoAssetId = null);

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
    DateTimeOffset CreatedAtUtc,
    Guid? CommercialPolicyVersionId = null);

public sealed record PlanningSummaryView(
    Guid BriefId,
    Guid BriefVersionId,
    string ClientName,
    string BriefTitle,
    string AudienceStatus,
    string? MediaMixStatus,
    string? MediaPlanStatus,
    DateTimeOffset UpdatedAtUtc);

public sealed record PlanningWorkspaceView(
    Guid BriefId,
    Guid BriefVersionId,
    string ClientName,
    CampaignModeSelectionView? CampaignMode,
    AudienceDefinitionSetView? Audience,
    MediaMixVersionView? MediaMix,
    InventoryShortlistVersionView? Shortlist,
    MediaPlanVersionView? MediaPlan);
