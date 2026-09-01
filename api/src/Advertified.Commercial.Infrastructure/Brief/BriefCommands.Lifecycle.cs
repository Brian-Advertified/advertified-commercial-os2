using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Brief;

public sealed partial class BriefCommands
{
    private async Task<CommandOutcome> SubmitOutcomeAsync(
        Guid versionId,
        CommandEnvelope<SubmitBriefVersionCommand> envelope,
        CancellationToken cancellationToken)
    {
        var row = await store.FindVersionAsync(envelope.TenantId, versionId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Brief access denied.");
        var brief = await store.FindBriefForUpdateAsync(
            envelope.TenantId, row.BriefId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Brief access denied.");
        EnsureOwner(brief, envelope.ActorId.Value);
        if (brief.CurrentDraftVersionId != versionId || row.Status != MasterDataCodes.LifecycleStatuses.Draft)
        {
            throw new InvalidLifecycleTransitionException();
        }
        EnsureNoCriticalConflict(row);
        var confirmerId = envelope.Command.ConfirmerUserId ?? envelope.ActorId.Value;
        await EnsureEligibleConfirmerAsync(envelope, confirmerId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.brief_versions
            SET status_code = {MasterDataCodes.LifecycleStatuses.InReview}, submitted_by = {envelope.ActorId.Value},
                submitted_at_utc = {now}, version = version + 1
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {versionId}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Draft} AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.campaign_briefs
            SET status_code = {MasterDataCodes.LifecycleStatuses.InReview}, version = version + 1,
                updated_at_utc = {now}
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {brief.Id}
            """, cancellationToken);
        await CreateApprovalTaskAsync(
            brief, row, confirmerId, now, cancellationToken);
        var view = row with
        {
            Status = MasterDataCodes.LifecycleStatuses.InReview,
            SubmittedBy = envelope.ActorId.Value,
            Version = row.Version + 1,
        };
        return OpportunityCommandSupport.Outcome(
            envelope, view.ToView(), versionId, view.Version,
            MasterDataReferences.CommercialResourceTypes.BriefVersion, MasterDataReferences.CommercialActions.BriefVersionSubmitted,
            MasterDataReferences.CommercialEventTypes.BriefSubmitted, now);
    }

    private async Task<CommandOutcome> MarkReadyOutcomeAsync(
        Guid versionId,
        CommandEnvelope<MarkBriefVersionReadyCommand> envelope,
        CancellationToken cancellationToken)
    {
        var row = await store.FindVersionAsync(envelope.TenantId, versionId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Brief access denied.");
        var brief = await store.FindBriefForUpdateAsync(
            envelope.TenantId, row.BriefId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Brief access denied.");
        EnsureOwner(brief, envelope.ActorId.Value);
        if (brief.CurrentDraftVersionId != versionId ||
            row.Status != MasterDataCodes.LifecycleStatuses.Draft)
        {
            throw new InvalidLifecycleTransitionException();
        }
        EnsurePlanningReady(row);
        var now = timeProvider.GetUtcNow();
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.brief_versions
            SET status_code = {MasterDataCodes.LifecycleStatuses.Ready}, version = version + 1
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {versionId}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Draft}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.campaign_briefs
            SET status_code = {MasterDataCodes.LifecycleStatuses.Ready},
                ready_version_id = {versionId}, current_draft_version_id = {versionId},
                version = version + 1, updated_at_utc = {now}
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {brief.Id};
            UPDATE commercial.opportunities
            SET stage_code = {MasterDataCodes.LifecycleStatuses.Planning}, version = version + 1,
                updated_at_utc = {now}
            WHERE tenant_id = {envelope.TenantId.Value}
              AND id = {brief.OpportunityId}
              AND stage_code = {MasterDataCodes.LifecycleStatuses.BriefReady};
            """, cancellationToken);
        var view = row with
        {
            Status = MasterDataCodes.LifecycleStatuses.Ready,
            Version = row.Version + 1,
        };
        return OpportunityCommandSupport.Outcome(
            envelope, view.ToView(), versionId, view.Version,
            MasterDataReferences.CommercialResourceTypes.BriefVersion,
            MasterDataReferences.CommercialActions.BriefVersionReady,
            MasterDataReferences.CommercialEventTypes.BriefReady, now);
    }

    private async Task<CommandOutcome> ApproveOutcomeAsync(
        Guid versionId,
        CommandEnvelope<ApproveBriefVersionCommand> envelope,
        CancellationToken cancellationToken)
    {
        var (row, brief) = await LoadDecisionContextAsync(
            versionId, envelope.ActorId.Value, envelope.TenantId, cancellationToken);
        EnsureNoCriticalConflict(row);
        var now = timeProvider.GetUtcNow();
        await ChangeDecisionAsync(
            row, envelope.ExpectedVersion, MasterDataCodes.LifecycleStatuses.Approved,
            envelope.ActorId.Value, null, null, envelope.TenantId.Value, now,
            cancellationToken);
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.campaign_briefs
            SET status_code = {MasterDataCodes.LifecycleStatuses.Approved}, approved_version_id = {versionId},
                ready_version_id = {versionId}, current_draft_version_id = {versionId},
                version = version + 1,
                updated_at_utc = {now}
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {brief.Id};
            UPDATE commercial.opportunities
            SET stage_code = {MasterDataCodes.LifecycleStatuses.Planning}, version = version + 1,
                updated_at_utc = {now}
            WHERE tenant_id = {envelope.TenantId.Value}
              AND id = {brief.OpportunityId}
              AND stage_code = {MasterDataCodes.LifecycleStatuses.BriefReady};
            """, cancellationToken);
        await CompleteApprovalTaskAsync(
            versionId, envelope.ActorId.Value, envelope.TenantId.Value, now, cancellationToken);
        var view = row with
        {
            Status = MasterDataCodes.LifecycleStatuses.Approved,
            ApprovedBy = envelope.ActorId.Value,
            Version = row.Version + 1,
        };
        return OpportunityCommandSupport.Outcome(
            envelope, view.ToView(), versionId, view.Version,
            MasterDataReferences.CommercialResourceTypes.BriefVersion, MasterDataReferences.CommercialActions.BriefVersionApproved,
            MasterDataReferences.CommercialEventTypes.BriefApproved, now);
    }

    private async Task<CommandOutcome> RejectOutcomeAsync(
        Guid versionId,
        CommandEnvelope<RejectBriefVersionCommand> envelope,
        CancellationToken cancellationToken)
    {
        var reason = OpportunityCommandSupport.Required(
            envelope.Command.Reason, 1000, nameof(envelope.Command.Reason));
        var requested = OpportunityCommandSupport.Required(
            envelope.Command.RequestedChanges, 2000,
            nameof(envelope.Command.RequestedChanges));
        var (row, brief) = await LoadDecisionContextAsync(
            versionId, envelope.ActorId.Value, envelope.TenantId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        await ChangeDecisionAsync(
            row, envelope.ExpectedVersion, MasterDataCodes.LifecycleStatuses.Rejected, null,
            envelope.ActorId.Value, reason, envelope.TenantId.Value, now,
            cancellationToken, requested);
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.campaign_briefs
            SET status_code = {MasterDataCodes.LifecycleStatuses.Rejected}, version = version + 1,
                updated_at_utc = {now}
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {brief.Id}
            """, cancellationToken);
        await CompleteApprovalTaskAsync(
            versionId, envelope.ActorId.Value, envelope.TenantId.Value, now, cancellationToken);
        var view = row with
        {
            Status = MasterDataCodes.LifecycleStatuses.Rejected,
            RejectedBy = envelope.ActorId.Value,
            RejectionReason = reason,
            RequestedChanges = requested,
            Version = row.Version + 1,
        };
        return OpportunityCommandSupport.Outcome(
            envelope, view.ToView(), versionId, view.Version,
            MasterDataReferences.CommercialResourceTypes.BriefVersion, MasterDataReferences.CommercialActions.BriefVersionRejected,
            MasterDataReferences.CommercialEventTypes.BriefRejected, now);
    }

    private async Task<(BriefVersionRow Version, CampaignBriefRow Brief)> LoadDecisionContextAsync(
        Guid versionId,
        Guid actorId,
        Advertified.Commercial.Domain.Governance.TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var row = await store.FindVersionAsync(tenantId, versionId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Brief confirmation denied.");
        var brief = await store.FindBriefForUpdateAsync(
            tenantId, row.BriefId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Brief confirmation denied.");
        var assigned = await store.DbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1 FROM commercial.human_tasks
                WHERE tenant_id = {tenantId.Value} AND resource_id = {versionId}
                  AND task_type_code = {MasterDataCodes.HumanTaskTypes.BriefApproval}
                  AND assignee_user_id = {actorId}
                  AND status_code = {MasterDataCodes.LifecycleStatuses.Pending}) AS "Value"
            """).SingleAsync(cancellationToken);
        if (!assigned || row.Status != MasterDataCodes.LifecycleStatuses.InReview ||
            brief.CurrentDraftVersionId != versionId)
        {
            throw new ApprovalRequiredException();
        }
        return (row, brief);
    }

    private async Task EnsureEligibleConfirmerAsync(
        CommandEnvelope<SubmitBriefVersionCommand> envelope,
        Guid confirmerId,
        CancellationToken cancellationToken)
    {
        var roles = await store.DbContext.Database.SqlQuery<string>($"""
            SELECT role_code AS "Value" FROM commercial.memberships
            WHERE tenant_id = {envelope.TenantId.Value} AND user_id = {confirmerId}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Active}
              AND role_code = ANY({OpportunityReviewerRoles.Brief})
            """).ToListAsync(cancellationToken);
        if (roles.Count == 0)
        {
            throw new ApprovalRequiredException();
        }
    }

    private static void EnsurePlanningReady(BriefVersionRow row)
    {
        var view = row.ToView();
        if (row.BudgetUnknown || !row.BudgetMinor.HasValue ||
            string.IsNullOrWhiteSpace(row.Currency) ||
            view.Unknowns.Any(item => item.IsBlocking))
        {
            throw new InvalidLifecycleTransitionException();
        }
        EnsureNoCriticalConflict(row);
    }

    private static void EnsureNoCriticalConflict(BriefVersionRow row)
    {
        if (row.ToView().Conflicts.Any(item =>
                item.Severity == MasterDataCodes.CriticSeverities.Critical && !item.Resolved))
        {
            throw new ApprovalRequiredException();
        }
    }

    private Task<int> CreateApprovalTaskAsync(
        CampaignBriefRow brief,
        BriefVersionRow row,
        Guid assignee,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.human_tasks (
                id, tenant_id, opportunity_id, task_type_code, status_code, title,
                why_it_matters, resource_type_code, resource_id, resource_version,
                assignee_user_id, action_schema_json, version, created_at_utc)
            VALUES (
                {Guid.NewGuid()}, {brief.TenantId}, {brief.OpportunityId},
                {MasterDataCodes.HumanTaskTypes.BriefApproval}, {MasterDataCodes.LifecycleStatuses.Pending},
                {"Confirm the campaign brief"},
                {"Confirm the exact version before planning begins."},
                {MasterDataReferences.CommercialResourceTypes.BriefVersion.Value}, {row.Id}, {row.Version + 1},
                {assignee}, {"{}"}::jsonb, 1, {now})
            """, cancellationToken);

    private Task<int> CompleteApprovalTaskAsync(
        Guid versionId,
        Guid actorId,
        Guid tenantId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.human_tasks
            SET status_code = {MasterDataCodes.LifecycleStatuses.Completed}, completed_by = {actorId},
                completed_at_utc = {now}, completion_json = {"{\"completed\":true}"}::jsonb,
                version = version + 1
            WHERE tenant_id = {tenantId} AND resource_id = {versionId}
              AND task_type_code = {MasterDataCodes.HumanTaskTypes.BriefApproval}
              AND assignee_user_id = {actorId} AND status_code = {MasterDataCodes.LifecycleStatuses.Pending}
            """, cancellationToken);

    private async Task ChangeDecisionAsync(
        BriefVersionRow row,
        long expectedVersion,
        string status,
        Guid? approvedBy,
        Guid? rejectedBy,
        string? reason,
        Guid tenantId,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        string? requestedChanges = null)
    {
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.brief_versions
            SET status_code = {status}, approved_by = {approvedBy},
                approved_at_utc = {(approvedBy.HasValue ? now : (DateTimeOffset?)null)},
                rejected_by = {rejectedBy},
                rejected_at_utc = {(rejectedBy.HasValue ? now : (DateTimeOffset?)null)},
                rejection_reason = {reason}, requested_changes = {requestedChanges},
                version = version + 1
            WHERE tenant_id = {tenantId} AND id = {row.Id}
              AND status_code = {MasterDataCodes.LifecycleStatuses.InReview} AND version = {expectedVersion}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
    }
}
