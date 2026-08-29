using Advertified.Commercial.Domain.Governance;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Opportunity;

public sealed partial class OpportunityRecordStore
{
    internal Task<List<OpportunityRow>> ListOpportunitiesAsync(
        TenantId tenantId,
        Guid actorId,
        int limit,
        int offset,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<OpportunityRow>($"""
            SELECT opportunity.id AS "Id", opportunity.tenant_id AS "TenantId",
                opportunity.client_account_id AS "ClientId", opportunity.title AS "Title",
                opportunity.source_type_code AS "SourceType",
                opportunity.source_ref AS "SourceRef",
                opportunity.owner_user_id AS "OwnerUserId", opportunity.stage_code AS "Stage",
                opportunity.expected_value_minor AS "ExpectedValueMinor",
                opportunity.currency_code AS "Currency", opportunity.deadline AS "Deadline",
                opportunity.problem_summary AS "ProblemSummary",
                opportunity.objective_summary AS "ObjectiveSummary",
                opportunity.version AS "Version", opportunity.updated_at_utc AS "UpdatedAtUtc"
            FROM commercial.opportunities opportunity
            WHERE opportunity.tenant_id = {tenantId.Value}
              AND (
                opportunity.owner_user_id = {actorId}
                OR EXISTS (
                    SELECT 1 FROM commercial.client_account_assignments assignment
                    WHERE assignment.tenant_id = opportunity.tenant_id
                      AND assignment.client_account_id = opportunity.client_account_id
                      AND assignment.user_id = {actorId}
                      AND assignment.effective_from_utc <= now()
                      AND (assignment.effective_to_utc IS NULL OR assignment.effective_to_utc > now()))
                OR EXISTS (
                    SELECT 1 FROM commercial.human_tasks task
                    WHERE task.tenant_id = opportunity.tenant_id
                      AND task.opportunity_id = opportunity.id
                      AND task.assignee_user_id = {actorId}))
            ORDER BY opportunity.updated_at_utc DESC, opportunity.id
            OFFSET {offset} LIMIT {limit}
            """).ToListAsync(cancellationToken);

    internal Task<List<EvidenceSourceRow>> ListSourcesAsync(
        TenantId tenantId,
        Guid opportunityId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<EvidenceSourceRow>($"""
            SELECT source.id AS "Id", link.opportunity_id AS "OpportunityId",
                source.type_code AS "Type", source.locator AS "Locator",
                source.title AS "Title", source.content_hash AS "ContentHash",
                source.policy_code AS "PolicyBasis",
                source.capture_status_code AS "CaptureStatus", source.version AS "Version",
                source.captured_at_utc AS "CapturedAtUtc"
            FROM commercial.opportunity_evidence_sources link
            JOIN commercial.evidence_sources source
              ON source.tenant_id = link.tenant_id AND source.id = link.source_id
            WHERE link.tenant_id = {tenantId.Value} AND link.opportunity_id = {opportunityId}
            ORDER BY source.captured_at_utc, source.id
            """).ToListAsync(cancellationToken);

    internal Task<List<EvidenceItemRow>> ListEvidenceItemsAsync(
        TenantId tenantId,
        Guid opportunityId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<EvidenceItemRow>($"""
            SELECT id AS "Id", source_id AS "SourceId", locator AS "Locator",
                claim_type_code AS "ClaimType", original_value_json::text AS "OriginalValueJson",
                reviewed_value_json::text AS "ReviewedValueJson", excerpt AS "Excerpt",
                confidence AS "Confidence", review_status_code AS "ReviewStatus",
                decision_code AS "Decision", review_reason AS "ReviewReason",
                created_by AS "CreatedBy", reviewed_by AS "ReviewedBy", version AS "Version"
            FROM commercial.evidence_items
            WHERE tenant_id = {tenantId.Value} AND opportunity_id = {opportunityId}
            ORDER BY created_at_utc, id
            """).ToListAsync(cancellationToken);

    internal Task<EvidenceSetRow?> FindLatestEvidenceSetAsync(
        TenantId tenantId,
        Guid opportunityId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<EvidenceSetRow>($"""
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
            WHERE evidence_set.tenant_id = {tenantId.Value}
              AND evidence_set.opportunity_id = {opportunityId}
              AND evidence_set.version_no = (
                SELECT max(candidate.version_no) FROM commercial.evidence_sets candidate
                WHERE candidate.tenant_id = evidence_set.tenant_id
                  AND candidate.opportunity_id = evidence_set.opportunity_id)
            GROUP BY evidence_set.id
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<InterpretationRow?> FindLatestInterpretationAsync(
        TenantId tenantId,
        Guid opportunityId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<InterpretationRow>($"""
            SELECT id AS "Id", opportunity_id AS "OpportunityId",
                evidence_set_id AS "EvidenceSetId", version_no AS "VersionNumber",
                artifact_json::text AS "ArtifactJson",
                evidence_bindings_json::text AS "EvidenceBindingsJson",
                unknowns_json::text AS "UnknownsJson", assumptions_json::text AS "AssumptionsJson",
                status_code AS "Status", created_by AS "CreatedBy",
                confirmed_by AS "ConfirmedBy", version AS "Version"
            FROM commercial.business_interpretations
            WHERE tenant_id = {tenantId.Value} AND opportunity_id = {opportunityId}
            ORDER BY version_no DESC LIMIT 1
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<List<AngleRow>> ListLatestAnglesAsync(
        TenantId tenantId,
        Guid opportunityId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<AngleRow>($"""
            SELECT angle.id AS "Id", angle.angle_set_id AS "AngleSetId", angle.rank AS "Rank",
                angle.title AS "Title", angle.rationale AS "Rationale",
                angle.evidence_item_ids_json::text AS "EvidenceItemIdsJson",
                angle.confidence AS "Confidence", angle.status_code AS "Status",
                angle.selected_by AS "SelectedBy", angle.version AS "Version"
            FROM commercial.opportunity_angles angle
            JOIN commercial.opportunity_angle_sets angle_set
              ON angle_set.tenant_id = angle.tenant_id AND angle_set.id = angle.angle_set_id
            WHERE angle.tenant_id = {tenantId.Value}
              AND angle_set.opportunity_id = {opportunityId}
              AND angle_set.version_no = (
                SELECT max(candidate.version_no) FROM commercial.opportunity_angle_sets candidate
                WHERE candidate.tenant_id = angle_set.tenant_id
                  AND candidate.opportunity_id = angle_set.opportunity_id)
            ORDER BY angle.rank, angle.id
            """).ToListAsync(cancellationToken);
}
