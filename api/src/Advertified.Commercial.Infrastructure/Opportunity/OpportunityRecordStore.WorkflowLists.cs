using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.Constants;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Opportunity;

public sealed partial class OpportunityRecordStore
{
    internal Task<StrategyRow?> FindLatestStrategyAsync(
        TenantId tenantId,
        Guid opportunityId,
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
            WHERE tenant_id = {tenantId.Value} AND opportunity_id = {opportunityId}
            ORDER BY version_no DESC LIMIT 1
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<List<AgentRunRow>> ListRunsAsync(
        TenantId tenantId,
        Guid opportunityId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<AgentRunRow>($"""
            SELECT run.id AS "Id", run.opportunity_id AS "OpportunityId",
                run.run_kind_code AS "RunKind", run.status_code AS "Status",
                run.current_step_code AS "CurrentStep", run.attempts AS "Attempts",
                run.error_code AS "ErrorCode",
                COALESCE(sum(usage.incremental_cost_minor), 0)::bigint AS "IncrementalCostMinor",
                run.version AS "Version", run.updated_at_utc AS "UpdatedAtUtc"
            FROM commercial.agent_runs run
            LEFT JOIN commercial.ai_usage_ledger usage
              ON usage.tenant_id = run.tenant_id AND usage.run_id = run.id
            WHERE run.tenant_id = {tenantId.Value} AND run.opportunity_id = {opportunityId}
            GROUP BY run.id
            ORDER BY run.created_at_utc DESC, run.id
            """).ToListAsync(cancellationToken);

    internal Task<List<HumanTaskRow>> ListTasksAsync(
        TenantId tenantId,
        Guid actorId,
        int limit,
        int offset,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<HumanTaskRow>($"""
            SELECT id AS "Id", opportunity_id AS "OpportunityId",
                task_type_code AS "TaskType", status_code AS "Status", title AS "Title",
                why_it_matters AS "WhyItMatters", resource_type_code AS "ResourceType",
                resource_id AS "ResourceId", resource_version AS "ResourceVersion",
                assignee_user_id AS "AssigneeUserId", version AS "Version",
                created_at_utc AS "CreatedAtUtc"
            FROM commercial.human_tasks
            WHERE tenant_id = {tenantId.Value} AND assignee_user_id = {actorId}
            ORDER BY CASE WHEN status_code = {Gate4Statuses.Pending} THEN 0 ELSE 1 END,
                created_at_utc DESC, id
            OFFSET {offset} LIMIT {limit}
            """).ToListAsync(cancellationToken);

    internal Task<HumanTaskRow?> FindTaskAsync(
        TenantId tenantId,
        Guid actorId,
        Guid taskId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<HumanTaskRow>($"""
            SELECT id AS "Id", opportunity_id AS "OpportunityId",
                task_type_code AS "TaskType", status_code AS "Status", title AS "Title",
                why_it_matters AS "WhyItMatters", resource_type_code AS "ResourceType",
                resource_id AS "ResourceId", resource_version AS "ResourceVersion",
                assignee_user_id AS "AssigneeUserId", version AS "Version",
                created_at_utc AS "CreatedAtUtc"
            FROM commercial.human_tasks
            WHERE tenant_id = {tenantId.Value} AND assignee_user_id = {actorId}
              AND id = {taskId}
            """).SingleOrDefaultAsync(cancellationToken);
}
