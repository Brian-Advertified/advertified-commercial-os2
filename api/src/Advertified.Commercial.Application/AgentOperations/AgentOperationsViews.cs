using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.AgentOperations;

public sealed record AgentBudgetView(
    string AgentCode,
    string DisplayLabel,
    string Provider,
    string Model,
    long CostCapMinor,
    int UsageCount,
    long IncrementalCostMinor,
    DateTimeOffset? LastUsedAtUtc);

public sealed record AgentUsageView(
    Guid Id,
    string AgentCode,
    string WorkType,
    string Status,
    string Provider,
    string Model,
    long? Units,
    int? ToolCalls,
    long IncrementalCostMinor,
    DateTimeOffset RecordedAtUtc);

public sealed record AgentOperationalRunView(
    Guid Id,
    Guid? OpportunityId,
    Guid? CampaignId,
    string RunKind,
    string Status,
    string? CurrentStep,
    int Attempts,
    string? ErrorCode,
    long IncrementalCostMinor,
    DateTimeOffset UpdatedAtUtc);

public sealed record AgentOperationsView(
    string Currency,
    string Provider,
    bool LiveProviderEnabled,
    long TotalIncrementalCostMinor,
    int DurableRunCount,
    int AttentionRunCount,
    IReadOnlyList<AgentBudgetView> Agents,
    IReadOnlyList<AgentUsageView> RecentUsage,
    IReadOnlyList<AgentOperationalRunView> RecentRuns);

public sealed record AgentOperationSubjectView(
    string ResourceType,
    Guid ResourceId,
    long? Version);

public sealed record AgentOperationStepProgressView(
    int Order,
    Guid Id,
    string StepCode,
    string? AgentCode,
    string Status,
    int Attempts,
    string SafeMessageCode,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? Provider,
    string? Model,
    int ToolCalls,
    long IncrementalCostMinor);

public sealed record AgentOperationProgressView(
    Guid Id,
    Guid TenantId,
    string RunKind,
    AgentOperationSubjectView Subject,
    string Status,
    string? CurrentStep,
    IReadOnlyList<string> CompletedSteps,
    string? ReviewRequiredStep,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    int Attempts,
    string? ErrorCode,
    Guid? CorrelationId,
    string? Provider,
    string? Model,
    int ToolCalls,
    long IncrementalCostMinor,
    IReadOnlyList<AgentOperationStepProgressView> Steps);

public interface IAgentOperationsReader
{
    Task<AgentOperationsView> GetAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken);

    Task<AgentOperationProgressView> GetProgressAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid runId,
        CancellationToken cancellationToken);
}
