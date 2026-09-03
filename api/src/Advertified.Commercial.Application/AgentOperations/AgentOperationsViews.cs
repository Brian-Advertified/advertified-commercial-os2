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

public interface IAgentOperationsReader
{
    Task<AgentOperationsView> GetAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken);
}
