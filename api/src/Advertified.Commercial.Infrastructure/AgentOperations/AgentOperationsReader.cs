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

    public async Task<AgentOperationProgressView> GetProgressAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid runId,
        CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(actorId, tenantId, cancellationToken);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var run = await store.FindProgressRunAsync(tenantId, runId, cancellationToken);
        var extraction = false;
        var emailAutomation = false;
        var proposalReplan = false;
        if (run is null)
        {
            run = await store.FindInventoryExtractionProgressAsync(
                tenantId, runId, cancellationToken);
            extraction = run is not null;
        }
        if (run is null)
        {
            run = await store.FindEmailAutomationProgressAsync(
                tenantId, runId, cancellationToken);
            emailAutomation = run is not null;
        }
        if (run is null)
        {
            run = await store.FindProposalReplanProgressAsync(
                tenantId, runId, cancellationToken);
            proposalReplan = run is not null;
        }
        if (run is null)
            throw new UnauthorizedAccessException("Agent operation access denied.");
        var steps = extraction
            ? await store.ListInventoryExtractionProgressStepsAsync(
                tenantId, runId, cancellationToken)
            : emailAutomation
                ? await store.ListEmailAutomationProgressStepsAsync(
                    tenantId, runId, cancellationToken)
                : proposalReplan
                    ? await store.ListProposalReplanProgressStepsAsync(
                        tenantId, runId, cancellationToken)
                    : await store.ListProgressStepsAsync(
                        tenantId, runId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return BuildProgress(run, steps);
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

    private static AgentOperationProgressView BuildProgress(
        AgentOperationProgressRow run,
        IReadOnlyList<AgentOperationStepRow> rows)
    {
        var steps = rows.Select((row, index) => new AgentOperationStepProgressView(
            index + 1, row.Id, row.StepCode, row.AgentCode, row.Status, row.Attempts,
            row.StepCode, row.CreatedAtUtc, row.UpdatedAtUtc, row.CompletedAtUtc,
            row.Provider, row.Model, row.ToolCalls, row.IncrementalCostMinor)).ToArray();
        var latestUsage = rows.LastOrDefault(item => item.Provider is not null);
        return new AgentOperationProgressView(
            run.Id, run.TenantId, run.RunKind, Subject(run), run.Status, run.CurrentStep,
            steps.Where(item => item.Status == MasterDataCodes.LifecycleStatuses.Completed)
                .Select(item => item.StepCode).ToArray(),
            ReviewRequiredStep(steps), run.CreatedAtUtc, run.UpdatedAtUtc,
            run.CompletedAtUtc, run.Attempts, run.ErrorCode, run.CorrelationId,
            latestUsage?.Provider, latestUsage?.Model, steps.Sum(item => item.ToolCalls),
            steps.Sum(item => item.IncrementalCostMinor), steps);
    }

    private static AgentOperationSubjectView Subject(AgentOperationProgressRow run)
    {
        if (!string.IsNullOrWhiteSpace(run.SubjectResourceType) && run.SubjectResourceId.HasValue)
            return new(run.SubjectResourceType, run.SubjectResourceId.Value, run.InputVersion);
        if (run.OpportunityId.HasValue)
            return new(MasterDataCodes.CommercialResourceTypes.Opportunity,
                run.OpportunityId.Value, run.InputVersion);
        if (run.CampaignId.HasValue)
            return new(MasterDataCodes.CommercialResourceTypes.Campaign,
                run.CampaignId.Value, run.InputVersion);
        if (run.SuppliedBriefInterpretationId.HasValue)
            return new(MasterDataCodes.CommercialResourceTypes.AgentRun,
                run.SuppliedBriefInterpretationId.Value, run.InputVersion);
        throw new InvalidOperationException("Agent operation has no canonical subject resource.");
    }

    private static string? ReviewRequiredStep(
        IReadOnlyList<AgentOperationStepProgressView> steps) =>
        steps.FirstOrDefault(item => item.Status is
            MasterDataCodes.LifecycleStatuses.ReviewRequired or
            MasterDataCodes.LifecycleStatuses.WaitingForHuman or
            MasterDataCodes.LifecycleStatuses.Failed or
            MasterDataCodes.InventoryExtractionAttemptStatuses.ReconciliationRequired or
            MasterDataCodes.InventoryExtractionAttemptStatuses.FailedRetryable or
            MasterDataCodes.InventoryExtractionAttemptStatuses.FailedTerminal or
            MasterDataCodes.InventoryExtractionAttemptStatuses.TimedOut)?.StepCode;

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
