using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.CommercialSettings;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Proposal;

public sealed partial class ProposalCommands
{
    private async Task<CommandOutcome> SubmitForApprovalOutcomeAsync(
        Guid proposalVersionId,
        CommandEnvelope<SubmitProposalForApprovalCommand> envelope,
        CancellationToken cancellationToken)
    {
        var proposal = await LoadOwnedProposalAsync(
            proposalVersionId, envelope, cancellationToken);
        if (proposal.Status != MasterDataCodes.LifecycleStatuses.Draft ||
            proposal.ExpiryAtUtc <= timeProvider.GetUtcNow())
        {
            throw new InvalidLifecycleTransitionException();
        }
        var approver = envelope.Command.ApproverUserId;
        if (approver == Guid.Empty || approver == envelope.ActorId.Value)
        {
            throw new ApprovalRequiredException();
        }
        var decision = await authorizer.AuthorizeAsync(
            new ActorId(approver), envelope.TenantId,
            MasterDataReferences.Permissions.ProposalApprove, cancellationToken);
        if (!decision.IsAllowed)
        {
            throw new ApprovalRequiredException();
        }
        await EnsureProposalPlansCurrentAsync(
            envelope.TenantId, proposalVersionId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.proposal_versions
            SET status_code = {MasterDataCodes.LifecycleStatuses.InReview},
                approval_assignee_user_id = {approver},
                approval_requested_by = {envelope.ActorId.Value},
                approval_requested_at_utc = {now}, version = version + 1
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {proposalVersionId}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Draft}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
        var updated = proposal with
        {
            Status = MasterDataCodes.LifecycleStatuses.InReview,
            ApprovalAssigneeUserId = approver,
            ApprovalRequestedBy = envelope.ActorId.Value,
            ApprovalRequestedAtUtc = now,
            Version = proposal.Version + 1,
        };
        await CreateProposalApprovalTaskAsync(
            envelope.TenantId, updated, approver, now, cancellationToken);
        var view = await store.BuildViewAsync(
            envelope.TenantId, updated, cancellationToken);
        return ProposalOutcome(
            envelope, view, proposalVersionId, updated.Version,
            MasterDataReferences.CommercialActions.ProposalUpdated,
            MasterDataReferences.CommercialEventTypes.ProposalUpdated, now);
    }

    private async Task<CommandOutcome> ApproveWithGovernanceAsync(
        Guid proposalVersionId,
        CommandEnvelope<ApproveProposalCommand> envelope,
        CancellationToken cancellationToken)
    {
        var (proposal, brief) = await LoadApprovalContextAsync(
            proposalVersionId, envelope.TenantId, cancellationToken);
        if (proposal.ExpiryAtUtc <= timeProvider.GetUtcNow())
        {
            throw new InvalidLifecycleTransitionException();
        }
        var approvalMode = await ResolveApprovalModeAsync(
            proposalVersionId, proposal, brief, envelope, cancellationToken);
        await EnsureProposalPlansCurrentAsync(
            envelope.TenantId, proposalVersionId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var updated = await PersistProposalApprovalAsync(
            proposalVersionId, proposal, approvalMode, envelope, now, cancellationToken);
        var view = await store.BuildViewAsync(
            envelope.TenantId, updated, cancellationToken);
        return ProposalOutcome(
            envelope, view, proposalVersionId, updated.Version,
            MasterDataReferences.CommercialActions.ProposalApproved,
            MasterDataReferences.CommercialEventTypes.ProposalApproved, now);
    }

    private async Task<string> ResolveApprovalModeAsync(
        Guid proposalVersionId,
        ProposalRow proposal,
        PlanningReadyBriefReferenceRow brief,
        CommandEnvelope<ApproveProposalCommand> envelope,
        CancellationToken cancellationToken)
    {
        if (proposal.Status == MasterDataCodes.LifecycleStatuses.Draft)
        {
            if (brief.OwnerUserId != envelope.ActorId.Value)
            {
                throw new ApprovalRequiredException();
            }
            await CommercialApprovalPolicy.EnsureSelfApprovalAllowedAsync(
                store.DbContext, envelope.TenantId, cancellationToken);
            return MasterDataCodes.ApprovalModes.Self;
        }
        if (proposal.Status != MasterDataCodes.LifecycleStatuses.InReview)
        {
            throw new InvalidLifecycleTransitionException();
        }
        if (brief.OwnerUserId == envelope.ActorId.Value ||
            proposal.ApprovalAssigneeUserId != envelope.ActorId.Value ||
            proposal.ApprovalRequestedBy is null ||
            !await HasAssignedProposalApprovalAsync(
                envelope.TenantId, proposalVersionId,
                envelope.ActorId.Value, cancellationToken))
        {
            throw new ApprovalRequiredException();
        }
        return MasterDataCodes.ApprovalModes.Independent;
    }

    private async Task<ProposalRow> PersistProposalApprovalAsync(
        Guid proposalVersionId,
        ProposalRow proposal,
        string approvalMode,
        CommandEnvelope<ApproveProposalCommand> envelope,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.proposal_versions
            SET status_code = {MasterDataCodes.LifecycleStatuses.Approved},
                approved_by = {envelope.ActorId.Value}, approved_at_utc = {now},
                approval_mode_code = {approvalMode}, version = version + 1
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {proposalVersionId}
              AND status_code = {proposal.Status} AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
        if (approvalMode == MasterDataCodes.ApprovalModes.Independent)
        {
            await CompleteProposalApprovalTaskAsync(
                envelope.TenantId, proposalVersionId, envelope.ActorId.Value,
                now, cancellationToken);
        }
        return proposal with
        {
            Status = MasterDataCodes.LifecycleStatuses.Approved,
            ApprovedBy = envelope.ActorId.Value,
            ApprovalMode = approvalMode,
            Version = proposal.Version + 1,
        };
    }

    private async Task<CommandOutcome> RejectApprovalOutcomeAsync(
        Guid proposalVersionId,
        CommandEnvelope<RejectProposalApprovalCommand> envelope,
        CancellationToken cancellationToken)
    {
        var reason = OpportunityCommandSupport.Required(
            envelope.Command.Reason, 1000, nameof(envelope.Command.Reason));
        var (proposal, brief) = await LoadApprovalContextAsync(
            proposalVersionId, envelope.TenantId, cancellationToken);
        if (proposal.Status != MasterDataCodes.LifecycleStatuses.InReview ||
            brief.OwnerUserId == envelope.ActorId.Value ||
            proposal.ApprovalAssigneeUserId != envelope.ActorId.Value ||
            !await HasAssignedProposalApprovalAsync(
                envelope.TenantId, proposalVersionId,
                envelope.ActorId.Value, cancellationToken))
        {
            throw new ApprovalRequiredException();
        }
        var now = timeProvider.GetUtcNow();
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.proposal_versions
            SET status_code = {MasterDataCodes.LifecycleStatuses.Rejected},
                approval_mode_code = {MasterDataCodes.ApprovalModes.Independent},
                approval_rejected_by = {envelope.ActorId.Value},
                approval_rejected_at_utc = {now},
                approval_rejection_reason = {reason}, version = version + 1
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {proposalVersionId}
              AND status_code = {MasterDataCodes.LifecycleStatuses.InReview}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
        await CompleteProposalApprovalTaskAsync(
            envelope.TenantId, proposalVersionId, envelope.ActorId.Value,
            now, cancellationToken);
        var updated = proposal with
        {
            Status = MasterDataCodes.LifecycleStatuses.Rejected,
            ApprovalMode = MasterDataCodes.ApprovalModes.Independent,
            ApprovalRejectedBy = envelope.ActorId.Value,
            ApprovalRejectedAtUtc = now,
            ApprovalRejectionReason = reason,
            Version = proposal.Version + 1,
        };
        var view = await store.BuildViewAsync(
            envelope.TenantId, updated, cancellationToken);
        return ProposalOutcome(
            envelope, view, proposalVersionId, updated.Version,
            MasterDataReferences.CommercialActions.ProposalRejected,
            MasterDataReferences.CommercialEventTypes.ProposalRejected, now);
    }

    private async Task<(ProposalRow Proposal, PlanningReadyBriefReferenceRow Brief)>
        LoadApprovalContextAsync(
            Guid proposalVersionId,
            TenantId tenantId,
            CancellationToken cancellationToken)
    {
        var proposal = await store.FindProposalAsync(
            tenantId, proposalVersionId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Proposal access denied.");
        var brief = await store.FindPlanningReadyBriefAsync(
            tenantId, proposal.BriefId, cancellationToken)
            ?? throw new ProposalStaleException();
        if (brief.BriefVersionId != proposal.BriefVersionId)
        {
            throw new ProposalStaleException();
        }
        return (proposal, brief);
    }

    private Task<bool> HasAssignedProposalApprovalAsync(
        TenantId tenantId,
        Guid proposalVersionId,
        Guid actorId,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1 FROM commercial.human_tasks task
                WHERE task.tenant_id = {tenantId.Value}
                  AND task.resource_id = {proposalVersionId}
                  AND task.task_type_code = {MasterDataCodes.HumanTaskTypes.ProposalApproval}
                  AND task.assignee_user_id = {actorId}
                  AND task.status_code = {MasterDataCodes.LifecycleStatuses.Pending}) AS "Value"
            """).SingleAsync(cancellationToken);

    private Task<int> CreateProposalApprovalTaskAsync(
        TenantId tenantId,
        ProposalRow proposal,
        Guid approver,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.human_tasks (
                id, tenant_id, opportunity_id, task_type_code, status_code, title,
                why_it_matters, resource_type_code, resource_id, resource_version,
                assignee_user_id, action_schema_json, version, created_at_utc)
            VALUES (
                {Guid.NewGuid()}, {tenantId.Value},
                (SELECT brief.opportunity_id FROM commercial.campaign_briefs brief
                 WHERE brief.tenant_id = {tenantId.Value} AND brief.id = {proposal.BriefId}),
                {MasterDataCodes.HumanTaskTypes.ProposalApproval},
                {MasterDataCodes.LifecycleStatuses.Pending},
                {"Approve the client proposal"},
                {"Review the exact proposal version before it can be rendered or shared."},
                {MasterDataReferences.CommercialResourceTypes.ProposalVersion.Value},
                {proposal.Id}, {proposal.Version}, {approver}, {"{}"}::jsonb, 1, {now})
            """, cancellationToken);

    private async Task CompleteProposalApprovalTaskAsync(
        TenantId tenantId,
        Guid proposalVersionId,
        Guid actorId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.human_tasks
            SET status_code = {MasterDataCodes.LifecycleStatuses.Completed},
                completed_by = {actorId}, completed_at_utc = {now},
                completion_json = {"{\"completed\":true}"}::jsonb,
                version = version + 1
            WHERE tenant_id = {tenantId.Value} AND resource_id = {proposalVersionId}
              AND task_type_code = {MasterDataCodes.HumanTaskTypes.ProposalApproval}
              AND assignee_user_id = {actorId}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Pending}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new ApprovalRequiredException();
        }
    }
}
