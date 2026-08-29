using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Foundation;

namespace Advertified.Commercial.Infrastructure.Opportunity;

public sealed class OpportunityReader(
    OpportunityRecordStore store,
    ITenantAuthorizer authorizer) : IOpportunityReader
{
    public async Task<CursorPage<OpportunityView>> ListAsync(
        ActorId actorId,
        TenantId tenantId,
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(actorId, tenantId, Gate4Permissions.OpportunityView, cancellationToken);
        var page = CursorPageFactory.Parse(limit, cursor);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var rows = await store.ListOpportunitiesAsync(
            tenantId, actorId.Value, page.Limit + 1, page.Offset, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CursorPageFactory.Create(
            rows.Select(OpportunityRowMapper.ToView).ToArray(), page.Limit, page.Offset);
    }

    public async Task<OpportunityDetailView> GetAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid opportunityId,
        CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(actorId, tenantId, Gate4Permissions.OpportunityView, cancellationToken);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        await EnsureResourceAccessAsync(actorId, tenantId, opportunityId, cancellationToken);
        var opportunity = await store.FindOpportunityAsync(tenantId, opportunityId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Opportunity access denied.");
        var sources = await store.ListSourcesAsync(tenantId, opportunityId, cancellationToken);
        var evidenceItems = await store.ListEvidenceItemsAsync(
            tenantId, opportunityId, cancellationToken);
        var evidenceSet = await store.FindLatestEvidenceSetAsync(
            tenantId, opportunityId, cancellationToken);
        var interpretation = await store.FindLatestInterpretationAsync(
            tenantId, opportunityId, cancellationToken);
        var angles = await store.ListLatestAnglesAsync(tenantId, opportunityId, cancellationToken);
        var strategy = await store.FindLatestStrategyAsync(tenantId, opportunityId, cancellationToken);
        var runs = await store.ListRunsAsync(tenantId, opportunityId, cancellationToken);
        var strategyView = strategy is null
            ? null
            : await ToStrategyViewAsync(tenantId, strategy, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new OpportunityDetailView(
            opportunity.ToView(),
            sources.Select(OpportunityRowMapper.ToView).ToArray(),
            evidenceItems.Select(OpportunityRowMapper.ToView).ToArray(),
            evidenceSet?.ToView(),
            interpretation?.ToView(),
            angles.Select(OpportunityRowMapper.ToView).ToArray(),
            strategyView,
            runs.Select(OpportunityRowMapper.ToView).ToArray(),
            NextAction(opportunity.Stage, interpretation, angles, strategy));
    }

    public async Task<StrategyVersionView> GetStrategyAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid strategyId,
        CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(actorId, tenantId, Gate4Permissions.StrategyView, cancellationToken);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var strategy = await store.FindStrategyAsync(tenantId, strategyId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Strategy access denied.");
        await EnsureResourceAccessAsync(
            actorId, tenantId, strategy.OpportunityId, cancellationToken);
        var view = await ToStrategyViewAsync(tenantId, strategy, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return view;
    }

    public async Task<AgentRunView> GetRunAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid runId,
        CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(actorId, tenantId, Gate4Permissions.RunView, cancellationToken);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var run = await store.FindRunAsync(tenantId, runId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Run access denied.");
        await EnsureResourceAccessAsync(actorId, tenantId, run.OpportunityId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return run.ToView();
    }

    public async Task<CursorPage<HumanTaskView>> ListTasksAsync(
        ActorId actorId,
        TenantId tenantId,
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(actorId, tenantId, Gate4Permissions.TaskView, cancellationToken);
        var page = CursorPageFactory.Parse(limit, cursor);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var rows = await store.ListTasksAsync(
            tenantId, actorId.Value, page.Limit + 1, page.Offset, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CursorPageFactory.Create(
            rows.Select(OpportunityRowMapper.ToView).ToArray(), page.Limit, page.Offset);
    }

    public async Task<HumanTaskView> GetTaskAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(actorId, tenantId, Gate4Permissions.TaskAct, cancellationToken);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var task = await store.FindTaskAsync(
            tenantId, actorId.Value, taskId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Task access denied.");
        await transaction.CommitAsync(cancellationToken);
        return task.ToView();
    }

    private async Task<StrategyVersionView> ToStrategyViewAsync(
        TenantId tenantId,
        StrategyRow strategy,
        CancellationToken cancellationToken)
    {
        var objections = await store.ListObjectionsAsync(
            tenantId, strategy.Id, cancellationToken);
        return new StrategyVersionView(
            strategy.Id, strategy.OpportunityId, strategy.VersionNumber, strategy.ArtifactJson,
            strategy.EvidenceBindingsJson, strategy.UnknownsJson, strategy.AssumptionsJson,
            strategy.Status, strategy.CreatedBy, strategy.SubmittedBy, strategy.ApprovedBy,
            strategy.RejectedBy, strategy.RejectionReason, strategy.Version,
            objections.Select(OpportunityRowMapper.ToView).ToArray());
    }

    private async Task EnsureResourceAccessAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid opportunityId,
        CancellationToken cancellationToken)
    {
        if (!await store.CanAccessOpportunityAsync(
                tenantId, actorId.Value, opportunityId, cancellationToken))
        {
            throw new UnauthorizedAccessException("Opportunity access denied.");
        }
    }

    private async Task EnsureAllowedAsync(
        ActorId actorId,
        TenantId tenantId,
        PermissionCode permission,
        CancellationToken cancellationToken)
    {
        var decision = await authorizer.AuthorizeAsync(
            actorId, tenantId, permission, cancellationToken);
        if (!decision.IsAllowed)
        {
            throw new UnauthorizedAccessException("Tenant access denied.");
        }
    }

    private static string NextAction(
        string stage,
        InterpretationRow? interpretation,
        List<AngleRow> angles,
        StrategyRow? strategy) => stage switch
        {
            Gate4Statuses.Created => "Register a source and start qualification.",
            Gate4Statuses.Qualifying => "Complete evidence review and submit the evidence set.",
            Gate4Statuses.EvidenceReview => "An assigned reviewer must approve the evidence set.",
            Gate4Statuses.StrategyReady when interpretation is null => "Run business interpretation.",
            Gate4Statuses.StrategyReady when interpretation.Status != Gate4Statuses.Approved =>
                "Confirm the business interpretation.",
            Gate4Statuses.StrategyReady when angles.Count == 0 => "Generate opportunity angles.",
            Gate4Statuses.StrategyReady when angles.All(
                item => item.Status != Gate4AngleStatuses.Selected) =>
                "Select an opportunity angle.",
            Gate4Statuses.StrategyReady when strategy is null => "Generate strategy and critic review.",
            Gate4Statuses.StrategyReady => "Resolve objections and approve the strategy.",
            Gate4Statuses.BriefReady => "Gate 4 is complete; Brief drafting belongs to Gate 5.",
            _ => "Review the current opportunity state.",
        };
}
