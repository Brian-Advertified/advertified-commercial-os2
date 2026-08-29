using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Advertified.Commercial.Infrastructure.Opportunity;

public sealed partial class OpportunityRecordStore(GovernanceDbContext dbContext)
{
    internal GovernanceDbContext DbContext => dbContext;

    internal async Task<IDbContextTransaction> BeginSessionAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await ApplicationDatabaseSession.SetAsync(
            dbContext,
            new UserId(actorId.Value),
            tenantId,
            cancellationToken);
        return transaction;
    }

    internal Task<OpportunityRow?> FindOpportunityAsync(
        TenantId tenantId,
        Guid opportunityId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<OpportunityRow>($"""
            SELECT id AS "Id", tenant_id AS "TenantId", client_account_id AS "ClientId",
                title AS "Title", source_type_code AS "SourceType", source_ref AS "SourceRef",
                owner_user_id AS "OwnerUserId", stage_code AS "Stage",
                expected_value_minor AS "ExpectedValueMinor", currency_code AS "Currency",
                deadline AS "Deadline", problem_summary AS "ProblemSummary",
                objective_summary AS "ObjectiveSummary", version AS "Version",
                updated_at_utc AS "UpdatedAtUtc"
            FROM commercial.opportunities
            WHERE tenant_id = {tenantId.Value} AND id = {opportunityId}
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<EvidenceItemRow?> FindEvidenceItemAsync(
        TenantId tenantId,
        Guid itemId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<EvidenceItemRow>($"""
            SELECT id AS "Id", source_id AS "SourceId", locator AS "Locator",
                claim_type_code AS "ClaimType", original_value_json::text AS "OriginalValueJson",
                reviewed_value_json::text AS "ReviewedValueJson", excerpt AS "Excerpt",
                confidence AS "Confidence", review_status_code AS "ReviewStatus",
                decision_code AS "Decision", review_reason AS "ReviewReason",
                created_by AS "CreatedBy", reviewed_by AS "ReviewedBy", version AS "Version"
            FROM commercial.evidence_items
            WHERE tenant_id = {tenantId.Value} AND id = {itemId}
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<EvidenceSourceRow?> FindSourceByHashAsync(
        TenantId tenantId,
        string type,
        string contentHash,
        Guid opportunityId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<EvidenceSourceRow>($"""
            SELECT source.id AS "Id", {opportunityId} AS "OpportunityId",
                source.type_code AS "Type", source.locator AS "Locator",
                source.title AS "Title", source.content_hash AS "ContentHash",
                source.policy_code AS "PolicyBasis",
                source.capture_status_code AS "CaptureStatus", source.version AS "Version",
                source.captured_at_utc AS "CapturedAtUtc"
            FROM commercial.evidence_sources source
            WHERE source.tenant_id = {tenantId.Value} AND source.type_code = {type}
              AND source.content_hash = {contentHash}
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<EvidenceSetRow?> FindEvidenceSetAsync(
        TenantId tenantId,
        Guid evidenceSetId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<EvidenceSetRow>($"""
            SELECT evidence_set.id AS "Id", evidence_set.opportunity_id AS "OpportunityId",
                evidence_set.version_no AS "VersionNumber",
                COALESCE(array_agg(link.evidence_item_id)
                    FILTER (WHERE link.evidence_item_id IS NOT NULL), ARRAY[]::uuid[])
                    AS "EvidenceItemIds",
                evidence_set.gaps_json::text AS "GapsJson", evidence_set.status_code AS "Status",
                evidence_set.created_by AS "CreatedBy", evidence_set.approved_by AS "ApprovedBy",
                evidence_set.version AS "Version"
            FROM commercial.evidence_sets evidence_set
            LEFT JOIN commercial.evidence_set_items link
                ON link.tenant_id = evidence_set.tenant_id
                AND link.evidence_set_id = evidence_set.id
            WHERE evidence_set.tenant_id = {tenantId.Value} AND evidence_set.id = {evidenceSetId}
            GROUP BY evidence_set.id
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<InterpretationRow?> FindInterpretationAsync(
        TenantId tenantId,
        Guid interpretationId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<InterpretationRow>($"""
            SELECT id AS "Id", opportunity_id AS "OpportunityId",
                evidence_set_id AS "EvidenceSetId", version_no AS "VersionNumber",
                artifact_json::text AS "ArtifactJson",
                evidence_bindings_json::text AS "EvidenceBindingsJson",
                unknowns_json::text AS "UnknownsJson", assumptions_json::text AS "AssumptionsJson",
                status_code AS "Status", created_by AS "CreatedBy",
                confirmed_by AS "ConfirmedBy", version AS "Version"
            FROM commercial.business_interpretations
            WHERE tenant_id = {tenantId.Value} AND id = {interpretationId}
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<AngleRow?> FindAngleAsync(
        TenantId tenantId,
        Guid angleId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<AngleRow>($"""
            SELECT id AS "Id", angle_set_id AS "AngleSetId", rank AS "Rank", title AS "Title",
                rationale AS "Rationale", evidence_item_ids_json::text AS "EvidenceItemIdsJson",
                confidence AS "Confidence", status_code AS "Status",
                selected_by AS "SelectedBy", version AS "Version"
            FROM commercial.opportunity_angles
            WHERE tenant_id = {tenantId.Value} AND id = {angleId}
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<AgentRunRow?> FindRunAsync(
        TenantId tenantId,
        Guid runId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<AgentRunRow>($"""
            SELECT run.id AS "Id", run.opportunity_id AS "OpportunityId",
                run.run_kind_code AS "RunKind", run.status_code AS "Status",
                run.current_step_code AS "CurrentStep", run.attempts AS "Attempts",
                run.error_code AS "ErrorCode",
                COALESCE(sum(usage.incremental_cost_minor), 0)::bigint AS "IncrementalCostMinor",
                run.version AS "Version", run.updated_at_utc AS "UpdatedAtUtc"
            FROM commercial.agent_runs run
            LEFT JOIN commercial.ai_usage_ledger usage
                ON usage.tenant_id = run.tenant_id AND usage.run_id = run.id
            WHERE run.tenant_id = {tenantId.Value} AND run.id = {runId}
            GROUP BY run.id
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<bool> HasAssignedTaskAsync(
        TenantId tenantId,
        Guid actorId,
        Guid resourceId,
        string taskType,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1 FROM commercial.human_tasks
                WHERE tenant_id = {tenantId.Value}
                  AND assignee_user_id = {actorId}
                  AND resource_id = {resourceId}
                  AND task_type_code = {taskType}
                  AND status_code = {Gate4Statuses.Pending}) AS "Value"
            """).SingleAsync(cancellationToken);

    internal Task<bool> IsOpportunityOwnerAsync(
        TenantId tenantId,
        Guid actorId,
        Guid opportunityId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1 FROM commercial.opportunities
                WHERE tenant_id = {tenantId.Value} AND id = {opportunityId}
                  AND owner_user_id = {actorId}) AS "Value"
            """).SingleAsync(cancellationToken);

    internal Task<bool> CanAccessOpportunityAsync(
        TenantId tenantId,
        Guid actorId,
        Guid opportunityId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1 FROM commercial.opportunities opportunity
                WHERE opportunity.tenant_id = {tenantId.Value}
                  AND opportunity.id = {opportunityId}
                  AND (
                    opportunity.owner_user_id = {actorId}
                    OR EXISTS (
                        SELECT 1 FROM commercial.client_account_assignments assignment
                        WHERE assignment.tenant_id = opportunity.tenant_id
                          AND assignment.client_account_id = opportunity.client_account_id
                          AND assignment.user_id = {actorId}
                          AND assignment.effective_from_utc <= now()
                          AND (assignment.effective_to_utc IS NULL
                            OR assignment.effective_to_utc > now()))
                    OR EXISTS (
                        SELECT 1 FROM commercial.human_tasks task
                        WHERE task.tenant_id = opportunity.tenant_id
                          AND task.opportunity_id = opportunity.id
                          AND task.assignee_user_id = {actorId}))) AS "Value"
            """).SingleAsync(cancellationToken);
}
