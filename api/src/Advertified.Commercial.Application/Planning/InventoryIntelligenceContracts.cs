using Advertified.Commercial.Application.Intelligence;

namespace Advertified.Commercial.Application.Planning;

public sealed record InventoryStrategyAudienceInput(
    Guid Id,
    string Name,
    string? NeedState,
    string? BuyingContext,
    IReadOnlyList<string> Geographies,
    string Classification,
    IReadOnlyList<string> Exclusions,
    IReadOnlyList<Guid> EvidenceItemIds);

public sealed record InventoryStrategyPeriodInput(
    DateOnly Start,
    DateOnly End);

public sealed record InventoryStrategyAllocationInput(
    string Channel,
    long BudgetMinor,
    string Role,
    IReadOnlyList<InventoryStrategyPeriodInput> RunningPeriods);

public sealed record InventoryStrategyInput(
    Guid AudienceArtifactId,
    long AudienceArtifactVersion,
    Guid MediaStrategyArtifactId,
    long MediaStrategyArtifactVersion,
    Guid MediaMixVersionId,
    long MediaMixVersion,
    string Objective,
    string? TargetingRationale,
    string? PositioningStatement,
    IReadOnlyList<InventoryStrategyAudienceInput> Audiences,
    IReadOnlyList<InventoryStrategyAllocationInput> Allocations);

public sealed record InventoryCandidateInput(
    Guid CandidateId,
    Guid ProductVersionId,
    string Name,
    string Channel,
    string Geography,
    long? RateAmountMinor,
    string? Currency,
    bool IsEligible,
    string? RejectionReason,
    string? RejectionDetail,
    decimal? Score,
    InventoryAudienceFitInput AudienceFit,
    InventorySuitabilityInput Suitability,
    InventoryBenchmarkInput? Benchmark);

public sealed record InventoryAudienceFitInput(
    decimal? LanguageScore,
    decimal? LifeStageScore,
    decimal? LsmSemScore,
    IReadOnlyList<string> EvidenceGaps,
    string? MeasurementSource,
    string? MeasurementPeriod,
    string? Methodology,
    string? TaxonomyName,
    string? TaxonomyVersion,
    IReadOnlyList<InventoryDeliveryMeasurementInput> DeliveryMeasurements,
    IReadOnlyList<string> DeliveryEvidenceGaps);

public sealed record InventoryDeliveryMeasurementInput(
    string MetricType,
    decimal? Value,
    string? Unit,
    string? Universe,
    string? MeasurementSource,
    string? MeasurementPeriod,
    string? Methodology,
    string? Limitations);

public sealed record InventorySuitabilityInput(
    string PolicyVersion,
    decimal Geography,
    decimal AudienceContext,
    decimal ObjectiveFormat,
    decimal BudgetEfficiency,
    decimal EvidenceQualityFreshness,
    decimal PortfolioCoverageDiversity,
    decimal Total,
    IReadOnlyList<string> EvidenceGaps,
    InventoryBuyAssessmentInput? BuyAssessment);

public sealed record InventoryDigitalExposureInput(
    int? SpotLengthSeconds,
    int? SlotLengthSeconds,
    int? LoopLengthSeconds,
    int? PlaysPerLoop,
    decimal? LoopSharePercent);

public sealed record InventoryPlannerAudienceContextInput(
    string Name,
    string? NeedState,
    string? BuyingContext);

public sealed record InventoryPlannerReasoningInput(
    string? PlannedChannelRole,
    IReadOnlyList<InventoryPlannerAudienceContextInput> TargetContexts,
    int RequiredPlacesMatched,
    int RequiredPlacesTotal,
    bool HasMeasuredTargetAudience,
    IReadOnlyList<string> ReviewQuestions,
    IReadOnlyList<string> SupportedReasons,
    IReadOnlyList<string> BuyingWarnings);

public sealed record InventoryBuyAssessmentInput(
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
    InventoryDigitalExposureInput? DigitalExposure,
    IReadOnlyList<string> EvidenceGaps,
    InventoryPlannerReasoningInput? PlannerReasoning);

public sealed record InventoryBenchmarkInput(
    string PolicyVersion,
    string GeographyBasis,
    int CohortSize,
    long? MedianMinor,
    decimal? Percentile,
    string Position,
    decimal Confidence,
    IReadOnlyList<string> Exclusions);

public sealed record InventoryIntelligenceInput(
    Guid TenantId,
    Guid ActorId,
    Guid RunId,
    Guid CorrelationId,
    Guid BriefVersionId,
    long BriefVersion,
    Guid ShortlistVersionId,
    long ShortlistVersion,
    IReadOnlyList<InventoryCandidateInput> Candidates,
    InventoryStrategyInput Strategy,
    IReadOnlyList<Guid> EvidenceItemIds);

public sealed record InventoryCandidateInterpretation(
    Guid CandidateId,
    string Rationale,
    string Classification);

public sealed record InventoryIntelligenceArtifact(
    IReadOnlyList<InventoryCandidateInterpretation> Interpretations);

public sealed record InventoryIntelligenceProposal(
    IReadOnlyList<InventoryCandidateInterpretation> Interpretations,
    IReadOnlyList<string> Unknowns,
    IReadOnlyList<string> Assumptions,
    string Rationale,
    IntelligenceInvocationUsage Usage);

public interface IInventoryIntelligenceAgentClient
{
    Task<InventoryIntelligenceProposal> InterpretShortlistAsync(
        InventoryIntelligenceInput input,
        CancellationToken cancellationToken);
}

public interface IInventoryIntelligenceService
{
    Task<IntelligenceArtifactView> AnalyseShortlistAsync(
        Guid actorId,
        Guid tenantId,
        Guid shortlistVersionId,
        CancellationToken cancellationToken);

    Task<IntelligenceArtifactView?> GetLatestAsync(
        Guid actorId,
        Guid tenantId,
        Guid shortlistVersionId,
        CancellationToken cancellationToken);
}
