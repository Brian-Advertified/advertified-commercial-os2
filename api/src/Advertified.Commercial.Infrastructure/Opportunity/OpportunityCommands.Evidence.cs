using System.Text.Json;
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

public sealed partial class OpportunityCommands
{
    public async Task<CommandResult<EvidenceItemView>> ReviewEvidenceItemAsync(
        Guid itemId,
        CommandEnvelope<ReviewEvidenceItemCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope,
            MasterDataReferences.Permissions.EvidenceReview,
            token => ReviewItemOutcomeAsync(itemId, envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<EvidenceItemView>(receipt);
    }

    public async Task<CommandResult<EvidenceSetView>> SubmitEvidenceAsync(
        Guid opportunityId,
        CommandEnvelope<SubmitEvidenceCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope,
            MasterDataReferences.Permissions.OpportunityEdit,
            token => SubmitEvidenceOutcomeAsync(opportunityId, envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<EvidenceSetView>(receipt);
    }

    public async Task<CommandResult<EvidenceSetView>> ApproveEvidenceSetAsync(
        Guid evidenceSetId,
        CommandEnvelope<ApproveEvidenceSetCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope,
            MasterDataReferences.Permissions.EvidenceReview,
            token => ApproveEvidenceOutcomeAsync(evidenceSetId, envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<EvidenceSetView>(receipt);
    }

    private async Task<CommandOutcome> ReviewItemOutcomeAsync(
        Guid itemId,
        CommandEnvelope<ReviewEvidenceItemCommand> envelope,
        CancellationToken cancellationToken)
    {
        var item = await store.FindEvidenceItemAsync(envelope.TenantId, itemId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Evidence access denied.");
        if (item.CreatedBy == envelope.ActorId.Value ||
            !await store.HasAssignedTaskAsync(
                envelope.TenantId, envelope.ActorId.Value, itemId,
                MasterDataCodes.HumanTaskTypes.EvidenceItemReview, cancellationToken))
        {
            throw new ApprovalRequiredException();
        }
        if (item.ReviewStatus != MasterDataCodes.LifecycleStatuses.Pending)
        {
            throw new InvalidLifecycleTransitionException();
        }

        var command = envelope.Command;
        var decision = OpportunityCommandSupport.Required(
            command.Decision, 100, nameof(command.Decision)).ToUpperInvariant();
        await OpportunityCommandSupport.EnsureCodeAsync(
            store.DbContext, MasterDataCodes.EvidenceReviewDecisions.Collection, decision, cancellationToken);
        var reviewed = ResolveReviewedValue(item, command, decision);
        var status = decision == MasterDataCodes.EvidenceReviewDecisions.Reject
            ? MasterDataCodes.LifecycleStatuses.Rejected
            : MasterDataCodes.LifecycleStatuses.Approved;
        var reason = OpportunityCommandSupport.Optional(command.Reason, 1000, nameof(command.Reason));
        if ((decision is MasterDataCodes.EvidenceReviewDecisions.Reject or MasterDataCodes.EvidenceReviewDecisions.Edit) && reason is null)
        {
            throw new ArgumentException("A review reason is required.");
        }

        var now = timeProvider.GetUtcNow();
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.evidence_items
            SET reviewed_value_json = {reviewed}::jsonb, review_status_code = {status},
                decision_collection_code = {MasterDataCodes.EvidenceReviewDecisions.Collection},
                decision_code = {decision},
                review_reason = {reason}, reviewed_by = {envelope.ActorId.Value},
                reviewed_at_utc = {now}, version = version + 1, updated_at_utc = {now}
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {itemId}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
        await OpportunityCommandSupport.CompleteTaskAsync(
            store.DbContext, envelope.TenantId, itemId, MasterDataCodes.HumanTaskTypes.EvidenceItemReview,
            envelope.ActorId.Value, now, cancellationToken);
        var view = item with
        {
            ReviewedValueJson = reviewed,
            ReviewStatus = status,
            Decision = decision,
            ReviewReason = reason,
            ReviewedBy = envelope.ActorId.Value,
            Version = item.Version + 1,
        };
        return OpportunityCommandSupport.Outcome(
            envelope, view.ToView(), itemId, view.Version, MasterDataReferences.CommercialResourceTypes.EvidenceItem,
            MasterDataReferences.CommercialActions.EvidenceReviewed, MasterDataReferences.CommercialEventTypes.EvidenceReviewed, now);
    }

    private async Task<CommandOutcome> SubmitEvidenceOutcomeAsync(
        Guid opportunityId,
        CommandEnvelope<SubmitEvidenceCommand> envelope,
        CancellationToken cancellationToken)
    {
        var opportunity = await EnsureOwnerAsync(envelope, opportunityId, cancellationToken);
        if (opportunity.Stage != MasterDataCodes.LifecycleStatuses.Qualifying)
        {
            throw new InvalidLifecycleTransitionException();
        }
        var pending = await CountEvidenceAsync(
            envelope.TenantId, opportunityId, MasterDataCodes.LifecycleStatuses.Pending, cancellationToken);
        var approvedIds = await ApprovedEvidenceIdsAsync(
            envelope.TenantId, opportunityId, cancellationToken);
        if (pending > 0 || approvedIds.Length == 0)
        {
            throw new EvidenceRequiredException();
        }
        await OpportunityCommandSupport.EnsureDifferentActiveReviewerAsync(
            store.DbContext, envelope.TenantId, envelope.ActorId.Value,
            envelope.Command.ApproverUserId, OpportunityReviewerRoles.Evidence.ToArray(), cancellationToken);

        var setId = Guid.NewGuid();
        var versionNumber = await NextEvidenceSetVersionAsync(
            envelope.TenantId, opportunityId, cancellationToken);
        var gapsJson = JsonSerializer.Serialize(
            envelope.Command.Gaps.Select(value => OpportunityCommandSupport.Required(
                value, 500, nameof(envelope.Command.Gaps))).ToArray());
        var now = timeProvider.GetUtcNow();
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.evidence_sets (
                id, tenant_id, opportunity_id, version_no, gaps_json, status_code,
                created_by, version, created_at_utc)
            VALUES (
                {setId}, {envelope.TenantId.Value}, {opportunityId}, {versionNumber},
                {gapsJson}::jsonb, {MasterDataCodes.LifecycleStatuses.InReview}, {envelope.ActorId.Value}, 1, {now})
            """, cancellationToken);
        foreach (var itemId in approvedIds)
        {
            await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO commercial.evidence_set_items (tenant_id, evidence_set_id, evidence_item_id)
                VALUES ({envelope.TenantId.Value}, {setId}, {itemId})
                """, cancellationToken);
        }
        await AdvanceOpportunityAsync(
            envelope, opportunityId, MasterDataCodes.LifecycleStatuses.EvidenceReview, now, cancellationToken);
        await OpportunityCommandSupport.CreateTaskAsync(
            store.DbContext, envelope.TenantId, opportunityId,
            MasterDataCodes.HumanTaskTypes.EvidenceSetApproval, "Approve the evidence set",
            "This exact evidence version controls every later interpretation and recommendation.",
            MasterDataReferences.CommercialResourceTypes.EvidenceSet, setId, 1, envelope.Command.ApproverUserId,
            now, cancellationToken);
        var view = new EvidenceSetView(
            setId, opportunityId, versionNumber, approvedIds,
            JsonSerializer.Deserialize<string[]>(gapsJson) ?? [], MasterDataCodes.LifecycleStatuses.InReview,
            envelope.ActorId.Value, null, 1);
        var opportunityVersion = opportunity.Version + 1;
        return OpportunityCommandSupport.Outcome(
            envelope, view, opportunityId, opportunityVersion, MasterDataReferences.CommercialResourceTypes.Opportunity,
            MasterDataReferences.CommercialActions.EvidenceSubmitted,
            MasterDataReferences.CommercialEventTypes.OpportunityEvidenceSubmitted, now);
    }

    private async Task<CommandOutcome> ApproveEvidenceOutcomeAsync(
        Guid evidenceSetId,
        CommandEnvelope<ApproveEvidenceSetCommand> envelope,
        CancellationToken cancellationToken)
    {
        var set = await store.FindEvidenceSetAsync(
            envelope.TenantId, evidenceSetId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Evidence access denied.");
        if (set.CreatedBy == envelope.ActorId.Value || set.Status != MasterDataCodes.LifecycleStatuses.InReview ||
            !await store.HasAssignedTaskAsync(
                envelope.TenantId, envelope.ActorId.Value, evidenceSetId,
                MasterDataCodes.HumanTaskTypes.EvidenceSetApproval, cancellationToken))
        {
            throw new ApprovalRequiredException();
        }

        var now = timeProvider.GetUtcNow();
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.evidence_sets
            SET status_code = {MasterDataCodes.LifecycleStatuses.Approved}, approved_by = {envelope.ActorId.Value},
                approved_at_utc = {now}, version = version + 1
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {evidenceSetId}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
        var opportunityChanged = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.opportunities
            SET stage_code = {MasterDataCodes.LifecycleStatuses.StrategyReady}, version = version + 1,
                updated_at_utc = {now}
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {set.OpportunityId}
              AND stage_code = {MasterDataCodes.LifecycleStatuses.EvidenceReview}
            """, cancellationToken);
        if (opportunityChanged != 1)
        {
            throw new InvalidLifecycleTransitionException();
        }
        await OpportunityCommandSupport.CompleteTaskAsync(
            store.DbContext, envelope.TenantId, evidenceSetId,
            MasterDataCodes.HumanTaskTypes.EvidenceSetApproval, envelope.ActorId.Value, now, cancellationToken);
        var view = set with
        {
            Status = MasterDataCodes.LifecycleStatuses.Approved,
            ApprovedBy = envelope.ActorId.Value,
            Version = set.Version + 1,
        };
        return OpportunityCommandSupport.Outcome(
            envelope, view.ToView(), evidenceSetId, view.Version,
            MasterDataReferences.CommercialResourceTypes.EvidenceSet, MasterDataReferences.CommercialActions.EvidenceApproved,
            MasterDataReferences.CommercialEventTypes.OpportunityEvidenceApproved, now);
    }

    private static string ResolveReviewedValue(
        EvidenceItemRow item,
        ReviewEvidenceItemCommand command,
        string decision)
    {
        if (decision == MasterDataCodes.EvidenceReviewDecisions.Edit)
        {
            return OpportunityCommandSupport.Json(
                command.StructuredValueJson ?? string.Empty,
                nameof(command.StructuredValueJson));
        }
        return item.OriginalValueJson;
    }

    private Task<int> CountEvidenceAsync(
        TenantId tenantId,
        Guid opportunityId,
        string status,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<int>($"""
            SELECT count(*)::integer AS "Value" FROM commercial.evidence_items
            WHERE tenant_id = {tenantId.Value} AND opportunity_id = {opportunityId}
              AND review_status_code = {status}
            """).SingleAsync(cancellationToken);

    private Task<Guid[]> ApprovedEvidenceIdsAsync(
        TenantId tenantId,
        Guid opportunityId,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<Guid>($"""
            SELECT id AS "Value" FROM commercial.evidence_items
            WHERE tenant_id = {tenantId.Value} AND opportunity_id = {opportunityId}
              AND review_status_code = {MasterDataCodes.LifecycleStatuses.Approved}
            ORDER BY id
            """).ToArrayAsync(cancellationToken);

    private Task<int> NextEvidenceSetVersionAsync(
        TenantId tenantId,
        Guid opportunityId,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<int>($"""
            SELECT COALESCE(max(version_no), 0)::integer + 1 AS "Value"
            FROM commercial.evidence_sets
            WHERE tenant_id = {tenantId.Value} AND opportunity_id = {opportunityId}
            """).SingleAsync(cancellationToken);

    private async Task AdvanceOpportunityAsync<TCommand>(
        CommandEnvelope<TCommand> envelope,
        Guid opportunityId,
        string stage,
        DateTimeOffset now,
        CancellationToken cancellationToken)
        where TCommand : notnull
    {
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.opportunities
            SET stage_code = {stage}, version = version + 1, updated_at_utc = {now}
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {opportunityId}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
    }
}
