using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.AgentOperations;

public sealed partial class AgentOperationsStore
{
    internal Task<AgentOperationProgressRow?> FindProgressRunAsync(
        TenantId tenantId,
        Guid runId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<AgentOperationProgressRow>($"""
            SELECT run.id AS "Id", run.tenant_id AS "TenantId",
                run.opportunity_id AS "OpportunityId", run.campaign_id AS "CampaignId",
                run.supplied_brief_interpretation_id AS "SuppliedBriefInterpretationId",
                NULL::text AS "SubjectResourceType", NULL::uuid AS "SubjectResourceId",
                run.input_version AS "InputVersion", run.run_kind_code AS "RunKind",
                run.status_code AS "Status", run.current_step_code AS "CurrentStep",
                run.attempts AS "Attempts", run.error_code AS "ErrorCode",
                run.correlation_id AS "CorrelationId", run.created_at_utc AS "CreatedAtUtc",
                run.updated_at_utc AS "UpdatedAtUtc", run.completed_at_utc AS "CompletedAtUtc"
            FROM commercial.agent_runs run
            WHERE run.tenant_id = {tenantId.Value} AND run.id = {runId}
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<List<AgentOperationStepRow>> ListProgressStepsAsync(
        TenantId tenantId,
        Guid runId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<AgentOperationStepRow>($"""
            SELECT step.id AS "Id", step.step_code AS "StepCode",
                step.agent_code AS "AgentCode", step.status_code AS "Status",
                step.attempt_count AS "Attempts", step.created_at_utc AS "CreatedAtUtc",
                step.updated_at_utc AS "UpdatedAtUtc",
                CASE WHEN step.status_code IN (
                    {MasterDataCodes.LifecycleStatuses.Completed},
                    {MasterDataCodes.LifecycleStatuses.Failed},
                    {MasterDataCodes.LifecycleStatuses.ReviewRequired},
                    {MasterDataCodes.LifecycleStatuses.Rejected},
                    {MasterDataCodes.LifecycleStatuses.Cancelled})
                    THEN COALESCE(step.checkpointed_at_utc, step.updated_at_utc)
                    ELSE NULL END AS "CompletedAtUtc",
                usage.provider_code AS "Provider", usage.model_code AS "Model",
                COALESCE(usage.tool_calls, 0)::integer AS "ToolCalls",
                COALESCE(usage.incremental_cost_minor, 0)::bigint AS "IncrementalCostMinor"
            FROM commercial.agent_run_steps step
            LEFT JOIN LATERAL (
                SELECT
                    (array_agg(item.provider_code ORDER BY item.recorded_at_utc DESC, item.id DESC))[1]
                        AS provider_code,
                    (array_agg(item.model_code ORDER BY item.recorded_at_utc DESC, item.id DESC))[1]
                        AS model_code,
                    sum(item.tool_calls)::integer AS tool_calls,
                    sum(item.incremental_cost_minor)::bigint AS incremental_cost_minor
                FROM commercial.ai_usage_ledger item
                WHERE item.tenant_id = step.tenant_id AND item.run_id = step.run_id
                  AND item.step_id = step.id) usage ON true
            WHERE step.tenant_id = {tenantId.Value} AND step.run_id = {runId}
            ORDER BY step.created_at_utc, step.id
            """).ToListAsync(cancellationToken);

    internal Task<AgentOperationProgressRow?> FindInventoryExtractionProgressAsync(
        TenantId tenantId,
        Guid attemptId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<AgentOperationProgressRow>($"""
            SELECT attempt.id AS "Id", attempt.tenant_id AS "TenantId",
                NULL::uuid AS "OpportunityId", NULL::uuid AS "CampaignId",
                NULL::uuid AS "SuppliedBriefInterpretationId",
                {MasterDataCodes.CommercialResourceTypes.InventoryImport}::text AS "SubjectResourceType",
                attempt.import_id AS "SubjectResourceId",
                attempt.source_file_version AS "InputVersion",
                {MasterDataCodes.CommercialResourceTypes.InventoryImport}::text AS "RunKind",
                attempt.status_code AS "Status", attempt.status_code AS "CurrentStep",
                attempt.attempt_number AS "Attempts",
                COALESCE(attempt.provider_error_code, attempt.failure_class_code) AS "ErrorCode",
                attempt.correlation_id AS "CorrelationId",
                attempt.created_at_utc AS "CreatedAtUtc", attempt.updated_at_utc AS "UpdatedAtUtc",
                attempt.completed_at_utc AS "CompletedAtUtc"
            FROM commercial.inventory_extraction_attempts attempt
            WHERE attempt.tenant_id = {tenantId.Value} AND attempt.id = {attemptId}
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<List<AgentOperationStepRow>> ListInventoryExtractionProgressStepsAsync(
        TenantId tenantId,
        Guid attemptId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<AgentOperationStepRow>($"""
            SELECT attempt.id AS "Id", {InventoryExtractionTraceCodes.OriginalSource}::text AS "StepCode",
                {MasterDataCodes.AgentTypes.InventoryIntelligence}::text AS "AgentCode",
                attempt.status_code AS "Status", attempt.attempt_number AS "Attempts",
                attempt.created_at_utc AS "CreatedAtUtc", attempt.updated_at_utc AS "UpdatedAtUtc",
                attempt.completed_at_utc AS "CompletedAtUtc",
                attempt.provider_name AS "Provider", attempt.provider_version AS "Model",
                0::integer AS "ToolCalls", 0::bigint AS "IncrementalCostMinor"
            FROM commercial.inventory_extraction_attempts attempt
            WHERE attempt.tenant_id = {tenantId.Value} AND attempt.id = {attemptId}
            UNION ALL
            SELECT semantic.id AS "Id",
                (COALESCE(semantic.request_json ->> 'operation', 'SEMANTIC') || ':' ||
                    semantic.chunk_number::text) AS "StepCode",
                {MasterDataCodes.AgentTypes.InventoryIntelligence}::text AS "AgentCode",
                semantic.status_code AS "Status", 1::integer AS "Attempts",
                semantic.created_at_utc AS "CreatedAtUtc",
                COALESCE(semantic.completed_at_utc, semantic.started_at_utc,
                    semantic.created_at_utc) AS "UpdatedAtUtc",
                semantic.completed_at_utc AS "CompletedAtUtc",
                'bedrock'::text AS "Provider", semantic.model_code AS "Model",
                0::integer AS "ToolCalls",
                ((COALESCE(semantic.incremental_cost_usd_micros, 0) + 9999) / 10000)::bigint
                    AS "IncrementalCostMinor"
            FROM commercial.inventory_semantic_runs semantic
            WHERE semantic.tenant_id = {tenantId.Value}
              AND semantic.extraction_attempt_id = {attemptId}
            ORDER BY "CreatedAtUtc", "Id"
            """).ToListAsync(cancellationToken);
}
