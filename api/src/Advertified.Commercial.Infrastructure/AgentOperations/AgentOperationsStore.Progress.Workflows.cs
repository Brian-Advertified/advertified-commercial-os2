using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.AgentOperations;

public sealed partial class AgentOperationsStore
{
    internal Task<AgentOperationProgressRow?> FindEmailAutomationProgressAsync(
        TenantId tenantId,
        Guid runId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<AgentOperationProgressRow>($"""
            SELECT run.id AS "Id", run.tenant_id AS "TenantId",
                NULL::uuid AS "OpportunityId", NULL::uuid AS "CampaignId",
                NULL::uuid AS "SuppliedBriefInterpretationId",
                {MasterDataCodes.CommercialResourceTypes.EmailProposalAutomationRun}::text
                    AS "SubjectResourceType",
                run.id AS "SubjectResourceId", run.version AS "InputVersion",
                {MasterDataCodes.CommercialResourceTypes.EmailProposalAutomationRun}::text
                    AS "RunKind",
                run.status_code AS "Status", run.checkpoint_code AS "CurrentStep",
                commercial.email_automation_attempts(run.tenant_id, run.id)
                    AS "Attempts",
                run.failure_code AS "ErrorCode",
                transition.correlation_id AS "CorrelationId",
                run.created_at_utc AS "CreatedAtUtc", run.updated_at_utc AS "UpdatedAtUtc",
                CASE WHEN run.status_code IN (
                    {MasterDataCodes.EmailAutomationStatuses.Sent},
                    {MasterDataCodes.EmailAutomationStatuses.Failed},
                    {MasterDataCodes.EmailAutomationStatuses.Duplicate})
                    THEN run.updated_at_utc ELSE NULL END AS "CompletedAtUtc"
            FROM commercial.email_proposal_automation_runs run
            LEFT JOIN LATERAL (
                SELECT audit.correlation_id
                FROM commercial.audit_events audit
                WHERE audit.tenant_id = run.tenant_id
                  AND audit.resource_type_code =
                      {MasterDataCodes.CommercialResourceTypes.EmailProposalAutomationRun}
                  AND audit.resource_id = run.id
                ORDER BY audit.occurred_at_utc, audit.id
                LIMIT 1) transition ON true
            WHERE run.tenant_id = {tenantId.Value} AND run.id = {runId}
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<List<AgentOperationStepRow>> ListEmailAutomationProgressStepsAsync(
        TenantId tenantId,
        Guid runId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<AgentOperationStepRow>($"""
            SELECT audit.id AS "Id", audit.action_code AS "StepCode",
                NULL::text AS "AgentCode",
                {MasterDataCodes.LifecycleStatuses.Completed}::text AS "Status",
                1::integer AS "Attempts",
                audit.occurred_at_utc AS "CreatedAtUtc",
                audit.occurred_at_utc AS "UpdatedAtUtc",
                audit.occurred_at_utc AS "CompletedAtUtc",
                NULL::text AS "Provider", NULL::text AS "Model",
                0::integer AS "ToolCalls", 0::bigint AS "IncrementalCostMinor"
            FROM commercial.audit_events audit
            WHERE audit.tenant_id = {tenantId.Value}
              AND audit.resource_type_code =
                  {MasterDataCodes.CommercialResourceTypes.EmailProposalAutomationRun}
              AND audit.resource_id = {runId}
            UNION ALL
            SELECT run.id AS "Id", run.checkpoint_code AS "StepCode",
                NULL::text AS "AgentCode", run.status_code AS "Status",
                commercial.email_automation_attempts(run.tenant_id, run.id)
                    AS "Attempts",
                run.created_at_utc AS "CreatedAtUtc", run.updated_at_utc AS "UpdatedAtUtc",
                CASE WHEN run.status_code IN (
                    {MasterDataCodes.EmailAutomationStatuses.Sent},
                    {MasterDataCodes.EmailAutomationStatuses.Failed},
                    {MasterDataCodes.EmailAutomationStatuses.Duplicate})
                    THEN run.updated_at_utc ELSE NULL END AS "CompletedAtUtc",
                NULL::text AS "Provider", NULL::text AS "Model",
                0::integer AS "ToolCalls", run.incremental_ai_cost_minor AS "IncrementalCostMinor"
            FROM commercial.email_proposal_automation_runs run
            WHERE run.tenant_id = {tenantId.Value} AND run.id = {runId}
            ORDER BY "CreatedAtUtc", "Id"
            """).ToListAsync(cancellationToken);

    internal Task<AgentOperationProgressRow?> FindProposalReplanProgressAsync(
        TenantId tenantId,
        Guid replanId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<AgentOperationProgressRow>($"""
            SELECT replan.id AS "Id", replan.tenant_id AS "TenantId",
                NULL::uuid AS "OpportunityId", NULL::uuid AS "CampaignId",
                NULL::uuid AS "SuppliedBriefInterpretationId",
                {MasterDataCodes.CommercialResourceTypes.ProposalReplanRevision}::text
                    AS "SubjectResourceType",
                replan.id AS "SubjectResourceId", replan.version AS "InputVersion",
                {MasterDataCodes.CommercialResourceTypes.ProposalReplanRevision}::text
                    AS "RunKind",
                replan.status_code AS "Status",
                {MasterDataCodes.CommercialResourceTypes.ProposalReplanRevision}::text
                    AS "CurrentStep",
                replan.attempts AS "Attempts", replan.error_code AS "ErrorCode",
                NULL::uuid AS "CorrelationId",
                replan.created_at_utc AS "CreatedAtUtc",
                replan.updated_at_utc AS "UpdatedAtUtc",
                replan.completed_at_utc AS "CompletedAtUtc"
            FROM commercial.proposal_replan_revisions replan
            WHERE replan.tenant_id = {tenantId.Value} AND replan.id = {replanId}
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<List<AgentOperationStepRow>> ListProposalReplanProgressStepsAsync(
        TenantId tenantId,
        Guid replanId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<AgentOperationStepRow>($"""
            SELECT replan.id AS "Id",
                {MasterDataCodes.CommercialResourceTypes.ProposalReplanRevision}::text AS "StepCode",
                NULL::text AS "AgentCode", replan.status_code AS "Status",
                replan.attempts AS "Attempts",
                replan.created_at_utc AS "CreatedAtUtc",
                replan.updated_at_utc AS "UpdatedAtUtc",
                replan.completed_at_utc AS "CompletedAtUtc",
                NULL::text AS "Provider", NULL::text AS "Model",
                0::integer AS "ToolCalls", 0::bigint AS "IncrementalCostMinor"
            FROM commercial.proposal_replan_revisions replan
            WHERE replan.tenant_id = {tenantId.Value} AND replan.id = {replanId}
            """).ToListAsync(cancellationToken);
}
