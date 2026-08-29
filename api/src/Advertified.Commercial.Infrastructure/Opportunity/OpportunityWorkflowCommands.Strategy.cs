using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Foundation;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Opportunity;

public sealed partial class OpportunityWorkflowCommands
{
    public async Task<CommandResult<CriticObjectionView>> ResolveObjectionAsync(
        Guid objectionId,
        CommandEnvelope<ResolveCriticObjectionCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope,
            Gate4Permissions.OpportunityEdit,
            token => ResolveObjectionOutcomeAsync(objectionId, envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<CriticObjectionView>(receipt);
    }

    public async Task<CommandResult<StrategyVersionView>> SubmitStrategyAsync(
        Guid strategyId,
        CommandEnvelope<SubmitStrategyCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope,
            Gate4Permissions.OpportunityEdit,
            token => SubmitStrategyOutcomeAsync(strategyId, envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<StrategyVersionView>(receipt);
    }

    public async Task<CommandResult<StrategyVersionView>> ApproveStrategyAsync(
        Guid strategyId,
        CommandEnvelope<ApproveStrategyCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope,
            Gate4Permissions.StrategyApprove,
            token => ApproveStrategyOutcomeAsync(strategyId, envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<StrategyVersionView>(receipt);
    }

    public async Task<CommandResult<StrategyVersionView>> RejectStrategyAsync(
        Guid strategyId,
        CommandEnvelope<RejectStrategyCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope,
            Gate4Permissions.StrategyApprove,
            token => RejectStrategyOutcomeAsync(strategyId, envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<StrategyVersionView>(receipt);
    }

    private async Task<CommandOutcome> ResolveObjectionOutcomeAsync(
        Guid objectionId,
        CommandEnvelope<ResolveCriticObjectionCommand> envelope,
        CancellationToken cancellationToken)
    {
        var objection = await store.FindObjectionAsync(
            envelope.TenantId, objectionId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Critic objection access denied.");
        var context = await FindObjectionContextAsync(
            envelope.TenantId, objectionId, cancellationToken);
        await EnsureOwnerAsync(envelope, context.OpportunityId, cancellationToken);
        if (objection.Resolution is not null ||
            !await store.HasAssignedTaskAsync(
                envelope.TenantId, envelope.ActorId.Value, objectionId,
                Gate4TaskTypes.CriticResolution, cancellationToken))
        {
            throw new ApprovalRequiredException();
        }
        var resolution = OpportunityCommandSupport.Required(
            envelope.Command.Resolution, 100, nameof(envelope.Command.Resolution))
            .ToUpperInvariant();
        await OpportunityCommandSupport.EnsureCodeAsync(
            store.DbContext, "objectionResolutions", resolution, cancellationToken);
        if (objection.Severity == Gate4CriticSeverities.Critical &&
            resolution == Gate4ObjectionResolutions.AcceptedWithReason)
        {
            throw new ApprovalRequiredException();
        }
        var reason = OpportunityCommandSupport.Required(
            envelope.Command.Reason, 1000, nameof(envelope.Command.Reason));
        var now = timeProvider.GetUtcNow();
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.critic_objections
            SET resolution_collection_code = 'objectionResolutions',
                resolution_code = {resolution}, resolution_reason = {reason},
                resolved_by = {envelope.ActorId.Value}, resolved_at_utc = {now},
                version = version + 1
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {objectionId}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
        await OpportunityCommandSupport.CompleteTaskAsync(
            store.DbContext, envelope.TenantId, objectionId, Gate4TaskTypes.CriticResolution,
            envelope.ActorId.Value, now, cancellationToken);
        var view = objection with
        {
            Resolution = resolution,
            ResolutionReason = reason,
            ResolvedBy = envelope.ActorId.Value,
            Version = objection.Version + 1,
        };
        return OpportunityCommandSupport.Outcome(
            envelope, view.ToView(), objectionId, view.Version,
            CommercialResourceTypes.Strategy, CommercialActions.ObjectionResolved,
            CommercialEventTypes.CriticObjectionResolved, now);
    }

    private async Task<CommandOutcome> SubmitStrategyOutcomeAsync(
        Guid strategyId,
        CommandEnvelope<SubmitStrategyCommand> envelope,
        CancellationToken cancellationToken)
    {
        var strategy = await store.FindStrategyAsync(
            envelope.TenantId, strategyId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Strategy access denied.");
        await EnsureOwnerAsync(envelope, strategy.OpportunityId, cancellationToken);
        if (strategy.Status != Gate4Statuses.Draft ||
            await store.HasUnresolvedObjectionsAsync(
                envelope.TenantId, strategyId, cancellationToken))
        {
            throw new ApprovalRequiredException();
        }
        var context = await FindStrategyContextAsync(
            envelope.TenantId, strategyId, cancellationToken);
        if (!context.ApproverUserId.HasValue ||
            context.ApproverUserId.Value == envelope.ActorId.Value)
        {
            throw new ApprovalRequiredException();
        }
        var now = timeProvider.GetUtcNow();
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.strategy_versions
            SET status_code = {Gate4Statuses.InReview}, submitted_by = {envelope.ActorId.Value},
                submitted_at_utc = {now}, version = version + 1
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {strategyId}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
        await OpportunityCommandSupport.CreateTaskAsync(
            store.DbContext, envelope.TenantId, strategy.OpportunityId,
            Gate4TaskTypes.StrategyApproval, "Approve the strategy",
            "Approval binds the exact evidence, selected angle and resolved critic report.",
            CommercialResourceTypes.Strategy, strategyId, strategy.Version + 1,
            context.ApproverUserId.Value, now, cancellationToken);
        var view = await StrategyViewAsync(
            strategy with
            {
                Status = Gate4Statuses.InReview,
                SubmittedBy = envelope.ActorId.Value,
                Version = strategy.Version + 1,
            },
            envelope.TenantId,
            cancellationToken);
        return OpportunityCommandSupport.Outcome(
            envelope, view, strategyId, view.Version, CommercialResourceTypes.Strategy,
            CommercialActions.StrategySubmitted, CommercialEventTypes.StrategySubmitted, now);
    }

    private async Task<CommandOutcome> ApproveStrategyOutcomeAsync(
        Guid strategyId,
        CommandEnvelope<ApproveStrategyCommand> envelope,
        CancellationToken cancellationToken)
    {
        var strategy = await store.FindStrategyAsync(
            envelope.TenantId, strategyId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Strategy access denied.");
        if (strategy.Status != Gate4Statuses.InReview ||
            strategy.CreatedBy == envelope.ActorId.Value ||
            await store.HasUnresolvedObjectionsAsync(
                envelope.TenantId, strategyId, cancellationToken) ||
            !await store.HasAssignedTaskAsync(
                envelope.TenantId, envelope.ActorId.Value, strategyId,
                Gate4TaskTypes.StrategyApproval, cancellationToken))
        {
            throw new ApprovalRequiredException();
        }
        var now = timeProvider.GetUtcNow();
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.strategy_versions
            SET status_code = {Gate4Statuses.Approved}, approved_by = {envelope.ActorId.Value},
                approved_at_utc = {now}, version = version + 1
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {strategyId}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
        var opportunityChanged = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.opportunities
            SET stage_code = {Gate4Statuses.BriefReady}, version = version + 1,
                updated_at_utc = {now}
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {strategy.OpportunityId}
              AND stage_code = {Gate4Statuses.StrategyReady}
            """, cancellationToken);
        if (opportunityChanged != 1)
        {
            throw new InvalidLifecycleTransitionException();
        }
        await OpportunityCommandSupport.CompleteTaskAsync(
            store.DbContext, envelope.TenantId, strategyId, Gate4TaskTypes.StrategyApproval,
            envelope.ActorId.Value, now, cancellationToken);
        var view = await StrategyViewAsync(
            strategy with
            {
                Status = Gate4Statuses.Approved,
                ApprovedBy = envelope.ActorId.Value,
                Version = strategy.Version + 1,
            },
            envelope.TenantId,
            cancellationToken);
        return OpportunityCommandSupport.Outcome(
            envelope, view, strategyId, view.Version, CommercialResourceTypes.Strategy,
            CommercialActions.StrategyApproved, CommercialEventTypes.StrategyApproved, now);
    }

    private async Task<CommandOutcome> RejectStrategyOutcomeAsync(
        Guid strategyId,
        CommandEnvelope<RejectStrategyCommand> envelope,
        CancellationToken cancellationToken)
    {
        var strategy = await store.FindStrategyAsync(
            envelope.TenantId, strategyId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Strategy access denied.");
        if (strategy.Status != Gate4Statuses.InReview ||
            strategy.CreatedBy == envelope.ActorId.Value ||
            !await store.HasAssignedTaskAsync(
                envelope.TenantId, envelope.ActorId.Value, strategyId,
                Gate4TaskTypes.StrategyApproval, cancellationToken))
        {
            throw new ApprovalRequiredException();
        }
        var reason = OpportunityCommandSupport.Required(
            envelope.Command.Reason, 1000, nameof(envelope.Command.Reason));
        var now = timeProvider.GetUtcNow();
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.strategy_versions
            SET status_code = {Gate4Statuses.Rejected}, rejected_by = {envelope.ActorId.Value},
                rejected_at_utc = {now}, rejection_reason = {reason}, version = version + 1
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {strategyId}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
        await OpportunityCommandSupport.CompleteTaskAsync(
            store.DbContext, envelope.TenantId, strategyId, Gate4TaskTypes.StrategyApproval,
            envelope.ActorId.Value, now, cancellationToken);
        var view = await StrategyViewAsync(
            strategy with
            {
                Status = Gate4Statuses.Rejected,
                RejectedBy = envelope.ActorId.Value,
                RejectionReason = reason,
                Version = strategy.Version + 1,
            },
            envelope.TenantId,
            cancellationToken);
        return OpportunityCommandSupport.Outcome(
            envelope, view, strategyId, view.Version, CommercialResourceTypes.Strategy,
            CommercialActions.StrategyRejected, CommercialEventTypes.StrategyRejected, now);
    }

    private async Task<StrategyVersionView> StrategyViewAsync(
        StrategyRow strategy,
        TenantId tenantId,
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

    private Task<ObjectionContextRow> FindObjectionContextAsync(
        TenantId tenantId,
        Guid objectionId,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<ObjectionContextRow>($"""
            SELECT strategy.id AS "StrategyId", strategy.opportunity_id AS "OpportunityId",
                strategy.created_by AS "StrategyCreatorId", run.approver_user_id AS "ApproverUserId"
            FROM commercial.critic_objections objection
            JOIN commercial.critic_reports report
              ON report.tenant_id = objection.tenant_id AND report.id = objection.critic_report_id
            JOIN commercial.strategy_versions strategy
              ON strategy.tenant_id = report.tenant_id AND strategy.id = report.strategy_version_id
            JOIN commercial.agent_runs run
              ON run.tenant_id = strategy.tenant_id AND run.id = strategy.agent_run_id
            WHERE objection.tenant_id = {tenantId.Value} AND objection.id = {objectionId}
            """).SingleAsync(cancellationToken);

    private Task<ObjectionContextRow> FindStrategyContextAsync(
        TenantId tenantId,
        Guid strategyId,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<ObjectionContextRow>($"""
            SELECT strategy.id AS "StrategyId", strategy.opportunity_id AS "OpportunityId",
                strategy.created_by AS "StrategyCreatorId", run.approver_user_id AS "ApproverUserId"
            FROM commercial.strategy_versions strategy
            JOIN commercial.agent_runs run
              ON run.tenant_id = strategy.tenant_id AND run.id = strategy.agent_run_id
            WHERE strategy.tenant_id = {tenantId.Value} AND strategy.id = {strategyId}
            """).SingleAsync(cancellationToken);
}
