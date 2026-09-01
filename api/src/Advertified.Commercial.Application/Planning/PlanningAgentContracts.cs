namespace Advertified.Commercial.Application.Planning;

public sealed record PlanningBriefInput(
    Guid TenantId,
    Guid ActorId,
    Guid RunId,
    Guid CorrelationId,
    Guid BriefVersionId,
    long BriefVersion,
    string Objective,
    IReadOnlyList<string> Audiences,
    IReadOnlyList<string> Geographies,
    IReadOnlyList<Guid> EvidenceItemIds);

public sealed record MediaPlanningInput(
    PlanningBriefInput Brief,
    long BudgetMinor,
    string Currency,
    IReadOnlyList<string> AvailableChannels);

public sealed record InventoryBenchmarkInput(
    string PolicyVersion,
    string GeographyBasis,
    int CohortSize,
    long? MedianMinor,
    decimal? Percentile,
    string Position,
    decimal Confidence,
    IReadOnlyList<string> Exclusions);

public sealed record InventoryIntelligenceCandidateInput(
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
    InventoryBenchmarkInput? Benchmark);

public sealed record InventoryIntelligenceInput(
    PlanningBriefInput Brief,
    Guid ShortlistVersionId,
    long ShortlistVersion,
    IReadOnlyList<InventoryIntelligenceCandidateInput> Candidates);

public sealed record AudienceDefinitionProposal(
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
    bool IsTarget);

public sealed record MediaAllocationProposal(
    string Channel,
    long BudgetMinor,
    string Role,
    IReadOnlyList<MediaRunningPeriodInput> RunningPeriods);

public sealed record AudienceAgentProposal(
    IReadOnlyList<AudienceDefinitionProposal> Audiences,
    string TargetingRationale,
    string PositioningStatement,
    IReadOnlyList<string> Unknowns,
    string Rationale,
    string Provider,
    string Model,
    long IncrementalCostMinor,
    string? ProviderRequestId = null);

public sealed record MediaPlanningAgentProposal(
    IReadOnlyList<MediaAllocationProposal> Allocations,
    IReadOnlyList<string> Unknowns,
    IReadOnlyList<string> Assumptions,
    string Rationale,
    string Provider,
    string Model,
    long IncrementalCostMinor,
    string? ProviderRequestId = null);

public sealed record InventoryCandidateInterpretationProposal(
    Guid CandidateId,
    string Rationale);

public sealed record InventoryIntelligenceAgentProposal(
    IReadOnlyList<InventoryCandidateInterpretationProposal> Interpretations,
    string Rationale,
    string Provider,
    string Model,
    long IncrementalCostMinor,
    string? ProviderRequestId = null);

public interface IPlanningAgentClient
{
    Task<AudienceAgentProposal> ProposeAudiencesAsync(
        PlanningBriefInput input,
        CancellationToken cancellationToken);

    Task<MediaPlanningAgentProposal> ProposeMediaMixAsync(
        MediaPlanningInput input,
        CancellationToken cancellationToken);

    Task<InventoryIntelligenceAgentProposal> InterpretInventoryAsync(
        InventoryIntelligenceInput input,
        CancellationToken cancellationToken);
}
