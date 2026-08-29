using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Foundation;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Opportunity;

public sealed partial class OpportunityWorkflowCommands(
    OpportunityRecordStore store,
    CommandDispatcher dispatcher,
    TimeProvider timeProvider) : IOpportunityWorkflowCommands
{
    public async Task<CommandResult<AgentRunView>> QueueRunAsync(
        Guid opportunityId,
        string runKind,
        CommandEnvelope<QueueAgentRunCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope,
            MasterDataReferences.Permissions.AgentRun,
            token => QueueRunOutcomeAsync(opportunityId, runKind, envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<AgentRunView>(receipt);
    }

    public async Task<CommandResult<BusinessInterpretationView>> ConfirmInterpretationAsync(
        Guid interpretationId,
        CommandEnvelope<ConfirmInterpretationCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope,
            MasterDataReferences.Permissions.AgentRun,
            token => ConfirmInterpretationOutcomeAsync(interpretationId, envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<BusinessInterpretationView>(receipt);
    }

    public async Task<CommandResult<OpportunityAngleView>> SelectAngleAsync(
        Guid angleId,
        CommandEnvelope<SelectOpportunityAngleCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope,
            MasterDataReferences.Permissions.OpportunityAngleSelect,
            token => SelectAngleOutcomeAsync(angleId, envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<OpportunityAngleView>(receipt);
    }

    public async Task<CommandResult<AgentRunView>> ManageRunAsync(
        Guid runId,
        bool cancel,
        CommandEnvelope<ManageAgentRunCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope,
            MasterDataReferences.Permissions.RunManage,
            token => ManageRunOutcomeAsync(runId, cancel, envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<AgentRunView>(receipt);
    }

    private async Task<CommandOutcome> QueueRunOutcomeAsync(
        Guid opportunityId,
        string runKind,
        CommandEnvelope<QueueAgentRunCommand> envelope,
        CancellationToken cancellationToken)
    {
        var kind = OpportunityCommandSupport.Required(runKind, 100, nameof(runKind))
            .ToUpperInvariant();
        if (kind is not (
            MasterDataCodes.AgentRunKinds.Interpretation or
            MasterDataCodes.AgentRunKinds.Angles or
            MasterDataCodes.AgentRunKinds.StrategyCritic or
            MasterDataCodes.AgentRunKinds.Brief))
        {
            throw new ArgumentException("The run kind is invalid.", nameof(runKind));
        }
        var opportunity = await EnsureOwnerAsync(envelope, opportunityId, cancellationToken);
        var expectedStage = kind == MasterDataCodes.AgentRunKinds.Brief
            ? MasterDataCodes.LifecycleStatuses.BriefReady
            : MasterDataCodes.LifecycleStatuses.StrategyReady;
        if (opportunity.Stage != expectedStage)
        {
            throw new InvalidLifecycleTransitionException();
        }
        await EnsureRunPrerequisitesAsync(envelope.TenantId, opportunityId, kind, cancellationToken);
        var activeRunExists = await store.DbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1
                FROM commercial.agent_runs
                WHERE tenant_id = {envelope.TenantId.Value}
                  AND opportunity_id = {opportunityId}
                  AND run_kind_code = {kind}
                  AND status_code IN (
                      {MasterDataCodes.LifecycleStatuses.Queued}, {MasterDataCodes.LifecycleStatuses.Running},
                      {MasterDataCodes.LifecycleStatuses.WaitingForHuman}, {MasterDataCodes.LifecycleStatuses.ReviewRequired})) AS "Value"
            """).SingleAsync(cancellationToken);
        if (activeRunExists)
        {
            throw new InvalidLifecycleTransitionException();
        }
        if (kind == MasterDataCodes.AgentRunKinds.StrategyCritic)
        {
            if (!envelope.Command.ApproverUserId.HasValue)
            {
                throw new ApprovalRequiredException();
            }
            await OpportunityCommandSupport.EnsureDifferentActiveReviewerAsync(
                store.DbContext, envelope.TenantId, envelope.ActorId.Value,
                envelope.Command.ApproverUserId.Value,
                OpportunityReviewerRoles.Strategy.ToArray(), cancellationToken);
        }

        var runId = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.agent_runs (
                id, tenant_id, opportunity_id, run_kind_code, status_code, input_version,
                requested_by, approver_user_id, correlation_id, attempts, version,
                created_at_utc, updated_at_utc)
            VALUES (
                {runId}, {envelope.TenantId.Value}, {opportunityId}, {kind},
                {MasterDataCodes.LifecycleStatuses.Queued}, {opportunity.Version}, {envelope.ActorId.Value},
                {envelope.Command.ApproverUserId}, {envelope.CorrelationId.Value}, 0, 1,
                {now}, {now})
            """, cancellationToken);
        var view = new AgentRunView(
            runId, opportunityId, kind, MasterDataCodes.LifecycleStatuses.Queued, null, 0, null, null, 0, 1, now);
        return OpportunityCommandSupport.Outcome(
            envelope, view, runId, 1, MasterDataReferences.CommercialResourceTypes.AgentRun,
            MasterDataReferences.CommercialActions.AgentRunQueued, MasterDataReferences.CommercialEventTypes.AgentRunQueued, now);
    }

    private async Task<CommandOutcome> ConfirmInterpretationOutcomeAsync(
        Guid interpretationId,
        CommandEnvelope<ConfirmInterpretationCommand> envelope,
        CancellationToken cancellationToken)
    {
        var row = await store.FindInterpretationAsync(
            envelope.TenantId, interpretationId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Interpretation access denied.");
        await EnsureOwnerAsync(envelope, row.OpportunityId, cancellationToken);
        if (row.Status != MasterDataCodes.LifecycleStatuses.Draft ||
            !await store.HasAssignedTaskAsync(
                envelope.TenantId, envelope.ActorId.Value, interpretationId,
                MasterDataCodes.HumanTaskTypes.InterpretationConfirmation, cancellationToken))
        {
            throw new ApprovalRequiredException();
        }
        var now = timeProvider.GetUtcNow();
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.business_interpretations
            SET status_code = {MasterDataCodes.LifecycleStatuses.Approved}, confirmed_by = {envelope.ActorId.Value},
                confirmed_at_utc = {now}, version = version + 1
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {interpretationId}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
        await OpportunityCommandSupport.CompleteTaskAsync(
            store.DbContext, envelope.TenantId, interpretationId,
            MasterDataCodes.HumanTaskTypes.InterpretationConfirmation, envelope.ActorId.Value, now,
            cancellationToken);
        var view = row with
        {
            Status = MasterDataCodes.LifecycleStatuses.Approved,
            ConfirmedBy = envelope.ActorId.Value,
            Version = row.Version + 1,
        };
        return OpportunityCommandSupport.Outcome(
            envelope, view.ToView(), interpretationId, view.Version,
            MasterDataReferences.CommercialResourceTypes.BusinessInterpretation, MasterDataReferences.CommercialActions.InterpretationConfirmed,
            MasterDataReferences.CommercialEventTypes.InterpretationConfirmed, now);
    }

    private async Task<CommandOutcome> SelectAngleOutcomeAsync(
        Guid angleId,
        CommandEnvelope<SelectOpportunityAngleCommand> envelope,
        CancellationToken cancellationToken)
    {
        var row = await store.FindAngleAsync(envelope.TenantId, angleId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Opportunity angle access denied.");
        var opportunityId = await OpportunityIdForAngleAsync(
            envelope.TenantId, angleId, cancellationToken);
        await EnsureOwnerAsync(envelope, opportunityId, cancellationToken);
        if (row.Status != MasterDataCodes.OpportunityAngleStatuses.Proposed ||
            !await store.HasAssignedTaskAsync(
                envelope.TenantId, envelope.ActorId.Value, row.AngleSetId,
                MasterDataCodes.HumanTaskTypes.AngleSelection, cancellationToken))
        {
            throw new ApprovalRequiredException();
        }
        var now = timeProvider.GetUtcNow();
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.opportunity_angles
            SET status_code = {MasterDataCodes.OpportunityAngleStatuses.Rejected}, version = version + 1
            WHERE tenant_id = {envelope.TenantId.Value} AND angle_set_id = {row.AngleSetId}
              AND status_code = {MasterDataCodes.OpportunityAngleStatuses.Proposed} AND id <> {angleId}
            """, cancellationToken);
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.opportunity_angles
            SET status_code = {MasterDataCodes.OpportunityAngleStatuses.Selected}, selected_by = {envelope.ActorId.Value},
                selected_at_utc = {now}, version = version + 1
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {angleId}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
        await OpportunityCommandSupport.CompleteTaskAsync(
            store.DbContext, envelope.TenantId, row.AngleSetId, MasterDataCodes.HumanTaskTypes.AngleSelection,
            envelope.ActorId.Value, now, cancellationToken);
        var view = row with
        {
            Status = MasterDataCodes.OpportunityAngleStatuses.Selected,
            SelectedBy = envelope.ActorId.Value,
            Version = row.Version + 1,
        };
        return OpportunityCommandSupport.Outcome(
            envelope, view.ToView(), angleId, view.Version,
            MasterDataReferences.CommercialResourceTypes.OpportunityAngle, MasterDataReferences.CommercialActions.OpportunityAngleSelected,
            MasterDataReferences.CommercialEventTypes.OpportunityAngleSelected, now);
    }

    private async Task<CommandOutcome> ManageRunOutcomeAsync(
        Guid runId,
        bool cancel,
        CommandEnvelope<ManageAgentRunCommand> envelope,
        CancellationToken cancellationToken)
    {
        var run = await store.FindRunAsync(envelope.TenantId, runId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Run access denied.");
        await EnsureOwnerAsync(envelope, run.OpportunityId, cancellationToken);
        var target = cancel ? MasterDataCodes.LifecycleStatuses.Cancelled : MasterDataCodes.LifecycleStatuses.Queued;
        if ((cancel && run.Status is MasterDataCodes.LifecycleStatuses.Completed or MasterDataCodes.LifecycleStatuses.Cancelled) ||
            (!cancel && run.Status is not (
                MasterDataCodes.LifecycleStatuses.ReviewRequired or MasterDataCodes.LifecycleStatuses.Failed or
                MasterDataCodes.LifecycleStatuses.WaitingForHuman)))
        {
            throw new RunNotResumableException();
        }
        var now = timeProvider.GetUtcNow();
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.agent_runs
            SET status_code = {target}, lease_owner = NULL, lease_expires_at_utc = NULL,
                next_attempt_at_utc = NULL, error_code = NULL, error_detail = NULL,
                version = version + 1, updated_at_utc = {now}
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {runId}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
        var view = run with { Status = target, ErrorCode = null, Version = run.Version + 1 };
        var action = cancel ? MasterDataReferences.CommercialActions.AgentRunCancelled : MasterDataReferences.CommercialActions.AgentRunResumed;
        var eventType = cancel
            ? MasterDataReferences.CommercialEventTypes.AgentRunCancelled
            : MasterDataReferences.CommercialEventTypes.AgentRunResumed;
        return OpportunityCommandSupport.Outcome(
            envelope, view.ToView(), runId, view.Version, MasterDataReferences.CommercialResourceTypes.AgentRun,
            action, eventType, now);
    }

    private async Task<OpportunityRow> EnsureOwnerAsync<TCommand>(
        CommandEnvelope<TCommand> envelope,
        Guid opportunityId,
        CancellationToken cancellationToken)
        where TCommand : notnull
    {
        var row = await store.FindOpportunityAsync(
            envelope.TenantId, opportunityId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Opportunity access denied.");
        if (row.OwnerUserId != envelope.ActorId.Value)
        {
            throw new UnauthorizedAccessException("Opportunity access denied.");
        }
        return row;
    }
}
