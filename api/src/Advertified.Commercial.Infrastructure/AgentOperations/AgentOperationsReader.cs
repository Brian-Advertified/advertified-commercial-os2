using Advertified.Commercial.Application.AgentOperations;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.AgentOperations;

public sealed class AgentOperationsReader(
    AgentOperationsStore store,
    ITenantAuthorizer authorizer,
    IOptions<AgentRuntimeOptions> runtimeOptions) : IAgentOperationsReader
{
    private const int RecentRecordLimit = 50;

    public async Task<AgentOperationsView> GetAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(actorId, tenantId, cancellationToken);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var agents = await store.ListAgentsAsync(cancellationToken);
        var summaries = await store.ListUsageSummariesAsync(tenantId, cancellationToken);
        var recentUsage = await store.ListRecentUsageAsync(
            tenantId, RecentRecordLimit, cancellationToken);
        var runSummary = await store.GetRunSummaryAsync(tenantId, cancellationToken);
        var recentRuns = await store.ListRecentRunsAsync(
            tenantId, RecentRecordLimit, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return BuildView(agents, summaries, recentUsage, runSummary, recentRuns);
    }

    private AgentOperationsView BuildView(
        List<AgentDefinitionRow> agents,
        List<AgentUsageSummaryRow> summaries,
        List<AgentUsageRow> recentUsage,
        AgentRunSummaryRow runSummary,
        List<AgentOperationalRunRow> recentRuns)
    {
        var settings = runtimeOptions.Value;
        var usageByAgent = summaries.ToDictionary(item => item.AgentCode, StringComparer.Ordinal);
        var budgets = agents.Select(agent => BuildBudget(agent, usageByAgent, settings)).ToArray();
        return new AgentOperationsView(
            MasterDataCodes.Currencies.Usd,
            settings.Provider,
            settings.AllowLive,
            budgets.Sum(item => item.IncrementalCostMinor),
            runSummary.DurableRunCount,
            runSummary.AttentionRunCount,
            budgets,
            recentUsage.Select(ToView).ToArray(),
            recentRuns.Select(ToView).ToArray());
    }

    private static AgentBudgetView BuildBudget(
        AgentDefinitionRow agent,
        Dictionary<string, AgentUsageSummaryRow> usageByAgent,
        AgentRuntimeOptions settings)
    {
        usageByAgent.TryGetValue(agent.AgentCode, out var usage);
        return new AgentBudgetView(
            agent.AgentCode,
            agent.DisplayLabel,
            settings.Provider,
            settings.ModelFor(agent.AgentCode),
            settings.CostCapFor(agent.AgentCode),
            usage?.UsageCount ?? 0,
            usage?.IncrementalCostMinor ?? 0,
            usage?.LastUsedAtUtc);
    }

    private static AgentUsageView ToView(AgentUsageRow row) => new(
        row.Id, row.AgentCode, row.WorkType, row.Status, row.Provider, row.Model,
        row.Units, row.ToolCalls, row.IncrementalCostMinor, row.RecordedAtUtc);

    private static AgentOperationalRunView ToView(AgentOperationalRunRow row) => new(
        row.Id, row.OpportunityId, row.CampaignId, row.RunKind, row.Status,
        row.CurrentStep, row.Attempts, row.ErrorCode, row.IncrementalCostMinor,
        row.UpdatedAtUtc);

    private async Task EnsureAllowedAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var decision = await authorizer.AuthorizeAsync(
            actorId,
            tenantId,
            MasterDataReferences.Permissions.CommercialSettingsView,
            cancellationToken);
        if (!decision.IsAllowed)
        {
            throw new UnauthorizedAccessException("Agent operations access denied.");
        }
    }
}
