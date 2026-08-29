using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.Constants;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Opportunity;

public sealed partial class OpportunityRecordStore
{
    internal Task<Guid?> FindBriefIdAsync(
        TenantId tenantId,
        Guid opportunityId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<Guid?>($"""
            SELECT id AS "Value" FROM commercial.campaign_briefs
            WHERE tenant_id = {tenantId.Value} AND opportunity_id = {opportunityId}
            """).SingleOrDefaultAsync(cancellationToken);

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
            SELECT task.id AS "Id", task.opportunity_id AS "OpportunityId",
                brief_version.brief_id AS "BriefId", task.task_type_code AS "TaskType",
                task.status_code AS "Status", task.title AS "Title",
                task.why_it_matters AS "WhyItMatters",
                task.resource_type_code AS "ResourceType", task.resource_id AS "ResourceId",
                task.resource_version AS "ResourceVersion",
                task.assignee_user_id AS "AssigneeUserId", task.version AS "Version",
                task.created_at_utc AS "CreatedAtUtc"
            FROM commercial.human_tasks task
            LEFT JOIN commercial.brief_versions brief_version
              ON brief_version.tenant_id = task.tenant_id
             AND brief_version.id = task.resource_id
            WHERE task.tenant_id = {tenantId.Value} AND task.assignee_user_id = {actorId}
            ORDER BY CASE WHEN task.status_code = {Gate4Statuses.Pending} THEN 0 ELSE 1 END,
                task.created_at_utc DESC, task.id
            OFFSET {offset} LIMIT {limit}
            """).ToListAsync(cancellationToken);

    internal Task<HumanTaskRow?> FindTaskAsync(
        TenantId tenantId,
        Guid actorId,
        Guid taskId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<HumanTaskRow>($"""
            SELECT task.id AS "Id", task.opportunity_id AS "OpportunityId",
                brief_version.brief_id AS "BriefId", task.task_type_code AS "TaskType",
                task.status_code AS "Status", task.title AS "Title",
                task.why_it_matters AS "WhyItMatters",
                task.resource_type_code AS "ResourceType", task.resource_id AS "ResourceId",
                task.resource_version AS "ResourceVersion",
                task.assignee_user_id AS "AssigneeUserId", task.version AS "Version",
                task.created_at_utc AS "CreatedAtUtc"
            FROM commercial.human_tasks task
            LEFT JOIN commercial.brief_versions brief_version
              ON brief_version.tenant_id = task.tenant_id
             AND brief_version.id = task.resource_id
            WHERE task.tenant_id = {tenantId.Value} AND task.assignee_user_id = {actorId}
              AND task.id = {taskId}
            """).SingleOrDefaultAsync(cancellationToken);
}
