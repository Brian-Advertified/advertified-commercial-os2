using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Advertified.Commercial.Infrastructure.AgentOperations;

public sealed partial class AgentOperationsStore(GovernanceDbContext dbContext)
{
    internal GovernanceDbContext DbContext => dbContext;

    internal async Task<IDbContextTransaction> BeginSessionAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await ApplicationDatabaseSession.SetAsync(
            dbContext, new UserId(actorId.Value), tenantId, cancellationToken);
        return transaction;
    }

    internal Task<List<AgentDefinitionRow>> ListAgentsAsync(
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<AgentDefinitionRow>($"""
            SELECT code AS "AgentCode", display_label AS "DisplayLabel",
                sort_order AS "SortOrder"
            FROM governance.master_data_items
            WHERE collection_code = {MasterDataCodes.AgentTypes.Collection}
              AND is_active = true
            ORDER BY sort_order, code
            """).ToListAsync(cancellationToken);

    internal Task<AgentRunSummaryRow> GetRunSummaryAsync(
        TenantId tenantId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<AgentRunSummaryRow>($"""
            SELECT count(*)::integer AS "DurableRunCount",
                count(*) FILTER (WHERE status_code IN (
                    {MasterDataCodes.LifecycleStatuses.ReviewRequired},
                    {MasterDataCodes.LifecycleStatuses.Failed}))::integer AS "AttentionRunCount"
            FROM commercial.agent_runs
            WHERE tenant_id = {tenantId.Value}
            """).SingleAsync(cancellationToken);

    internal Task<List<AgentOperationalRunRow>> ListRecentRunsAsync(
        TenantId tenantId,
        int limit,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<AgentOperationalRunRow>($"""
            SELECT run.id AS "Id", run.opportunity_id AS "OpportunityId",
                run.campaign_id AS "CampaignId", run.run_kind_code AS "RunKind",
                run.status_code AS "Status", run.current_step_code AS "CurrentStep",
                run.attempts AS "Attempts", run.error_code AS "ErrorCode",
                COALESCE(sum(usage.incremental_cost_minor), 0)::bigint
                    AS "IncrementalCostMinor",
                run.updated_at_utc AS "UpdatedAtUtc"
            FROM commercial.agent_runs run
            LEFT JOIN commercial.ai_usage_ledger usage
              ON usage.tenant_id = run.tenant_id AND usage.run_id = run.id
            WHERE run.tenant_id = {tenantId.Value}
            GROUP BY run.id
            ORDER BY run.updated_at_utc DESC, run.id
            LIMIT {limit}
            """).ToListAsync(cancellationToken);
}
