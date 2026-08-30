namespace Advertified.Commercial.Application.Planning;

public sealed record PlanningBriefInput(
    Guid TenantId,
    Guid ActorId,
    Guid BriefVersionId,
    string Objective,
    IReadOnlyList<string> Audiences,
    IReadOnlyList<string> Geographies,
    long BudgetMinor,
    string Currency,
    IReadOnlyList<Guid> EvidenceItemIds,
    IReadOnlyList<string> AvailableChannels);

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

public sealed record PlanningAgentProposal(
    IReadOnlyList<AudienceDefinitionProposal> Audiences,
    string TargetingRationale,
    string PositioningStatement,
    IReadOnlyList<MediaAllocationProposal> Allocations,
    IReadOnlyList<string> Unknowns,
    IReadOnlyList<string> Assumptions,
    string Rationale,
    string Provider,
    string Model,
    long IncrementalCostMinor);

public interface IPlanningAgentClient
{
    Task<PlanningAgentProposal> ProposeAsync(
        PlanningBriefInput input,
        CancellationToken cancellationToken);
}
