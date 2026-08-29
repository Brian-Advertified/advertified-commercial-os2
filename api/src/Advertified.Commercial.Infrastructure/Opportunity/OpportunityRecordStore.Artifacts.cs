using Advertified.Commercial.Domain.Governance;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Opportunity;

public sealed partial class OpportunityRecordStore
{
    internal Task<StrategyRow?> FindStrategyAsync(
        TenantId tenantId,
        Guid strategyId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<StrategyRow>($"""
            SELECT id AS "Id", opportunity_id AS "OpportunityId",
                version_no AS "VersionNumber", artifact_json::text AS "ArtifactJson",
                evidence_bindings_json::text AS "EvidenceBindingsJson",
                unknowns_json::text AS "UnknownsJson", assumptions_json::text AS "AssumptionsJson",
                status_code AS "Status", created_by AS "CreatedBy",
                submitted_by AS "SubmittedBy", approved_by AS "ApprovedBy",
                rejected_by AS "RejectedBy", rejection_reason AS "RejectionReason",
                version AS "Version"
            FROM commercial.strategy_versions
            WHERE tenant_id = {tenantId.Value} AND id = {strategyId}
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<List<ObjectionRow>> ListObjectionsAsync(
        TenantId tenantId,
        Guid strategyId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<ObjectionRow>($"""
            SELECT objection.id AS "Id", objection.severity_code AS "Severity",
                objection.field_path AS "FieldPath", objection.evidence_gap AS "EvidenceGap",
                objection.recommended_resolution AS "RecommendedResolution",
                objection.resolution_code AS "Resolution",
                objection.resolution_reason AS "ResolutionReason",
                objection.resolved_by AS "ResolvedBy", objection.version AS "Version"
            FROM commercial.critic_objections objection
            JOIN commercial.critic_reports report
                ON report.tenant_id = objection.tenant_id
                AND report.id = objection.critic_report_id
            WHERE objection.tenant_id = {tenantId.Value}
              AND report.strategy_version_id = {strategyId}
            ORDER BY objection.severity_code, objection.id
            """).ToListAsync(cancellationToken);

    internal Task<ObjectionRow?> FindObjectionAsync(
        TenantId tenantId,
        Guid objectionId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<ObjectionRow>($"""
            SELECT id AS "Id", severity_code AS "Severity", field_path AS "FieldPath",
                evidence_gap AS "EvidenceGap",
                recommended_resolution AS "RecommendedResolution",
                resolution_code AS "Resolution", resolution_reason AS "ResolutionReason",
                resolved_by AS "ResolvedBy", version AS "Version"
            FROM commercial.critic_objections
            WHERE tenant_id = {tenantId.Value} AND id = {objectionId}
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<bool> HasUnresolvedObjectionsAsync(
        TenantId tenantId,
        Guid strategyId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1
                FROM commercial.critic_objections objection
                JOIN commercial.critic_reports report
                  ON report.tenant_id = objection.tenant_id
                 AND report.id = objection.critic_report_id
                WHERE objection.tenant_id = {tenantId.Value}
                  AND report.strategy_version_id = {strategyId}
                  AND objection.resolution_code IS NULL) AS "Value"
            """).SingleAsync(cancellationToken);
}
