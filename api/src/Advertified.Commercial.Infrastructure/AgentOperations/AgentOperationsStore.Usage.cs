using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace Advertified.Commercial.Infrastructure.AgentOperations;

public sealed partial class AgentOperationsStore
{
    internal Task<List<AgentUsageSummaryRow>> ListUsageSummariesAsync(
        TenantId tenantId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<AgentUsageSummaryRow>(UsageSummaryQuery(tenantId))
            .ToListAsync(cancellationToken);

    internal Task<List<AgentUsageRow>> ListRecentUsageAsync(
        TenantId tenantId,
        int limit,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<AgentUsageRow>(RecentUsageQuery(tenantId, limit))
            .ToListAsync(cancellationToken);

    private static FormattableString UsageSummaryQuery(TenantId tenantId)
    {
        var records = UsageRecords(tenantId);
        var format = "WITH usage_records AS (" + records.Format + ") " +
            "SELECT \"AgentCode\", count(*)::integer AS \"UsageCount\", " +
            "sum(\"IncrementalCostMinor\")::bigint AS \"IncrementalCostMinor\", " +
            "max(\"RecordedAtUtc\") AS \"LastUsedAtUtc\" " +
            "FROM usage_records GROUP BY \"AgentCode\" ORDER BY \"AgentCode\"";
        return FormattableStringFactory.Create(format, records.GetArguments());
    }

    private static FormattableString RecentUsageQuery(TenantId tenantId, int limit)
    {
        var records = UsageRecords(tenantId);
        var arguments = records.GetArguments().Append(limit).ToArray();
        var format = "WITH usage_records AS (" + records.Format + ") " +
            "SELECT \"Id\", \"AgentCode\", \"WorkType\", \"Status\", \"Provider\", " +
            "\"Model\", \"Units\", \"ToolCalls\", \"IncrementalCostMinor\", " +
            "\"RecordedAtUtc\" FROM usage_records " +
            $"ORDER BY \"RecordedAtUtc\" DESC, \"Id\" LIMIT {{{arguments.Length - 1}}}";
        return FormattableStringFactory.Create(format, arguments);
    }

    private static FormattableString UsageRecords(TenantId tenantId) => $"""
        SELECT usage.id AS "Id", step.agent_code AS "AgentCode",
            run.run_kind_code AS "WorkType", step.status_code AS "Status",
            usage.provider_code AS "Provider", usage.model_code AS "Model",
            usage.units::bigint AS "Units", usage.tool_calls AS "ToolCalls",
            usage.incremental_cost_minor::bigint AS "IncrementalCostMinor",
            usage.recorded_at_utc AS "RecordedAtUtc"
        FROM commercial.ai_usage_ledger usage
        JOIN commercial.agent_run_steps step
          ON step.tenant_id = usage.tenant_id AND step.id = usage.step_id
        JOIN commercial.agent_runs run
          ON run.tenant_id = usage.tenant_id AND run.id = usage.run_id
        WHERE usage.tenant_id = {tenantId.Value}
        UNION ALL
        SELECT audience.id, {MasterDataCodes.AgentTypes.Audience},
            {MasterDataCodes.CommercialResourceTypes.AudienceDefinitionSet},
            audience.status_code, audience.agent_provider_code,
            audience.agent_model_code, NULL::bigint, NULL::integer,
            audience.agent_incremental_cost_minor, audience.created_at_utc
        FROM commercial.audience_definition_sets audience
        WHERE audience.tenant_id = {tenantId.Value}
          AND audience.agent_provider_code IS NOT NULL
        UNION ALL
        SELECT mix.id, {MasterDataCodes.AgentTypes.MediaPlanning},
            {MasterDataCodes.CommercialResourceTypes.MediaMixVersion}, mix.status_code,
            mix.agent_provider_code, mix.agent_model_code, NULL::bigint, NULL::integer,
            mix.agent_incremental_cost_minor, mix.created_at_utc
        FROM commercial.media_mix_versions mix
        WHERE mix.tenant_id = {tenantId.Value} AND mix.agent_provider_code IS NOT NULL
        UNION ALL
        SELECT shortlist.id, {MasterDataCodes.AgentTypes.InventoryIntelligence},
            {MasterDataCodes.CommercialResourceTypes.InventoryShortlistVersion},
            shortlist.status_code, shortlist.agent_provider_code,
            shortlist.agent_model_code, NULL::bigint, NULL::integer,
            shortlist.agent_incremental_cost_minor, shortlist.created_at_utc
        FROM commercial.inventory_shortlist_versions shortlist
        WHERE shortlist.tenant_id = {tenantId.Value}
          AND shortlist.agent_provider_code IS NOT NULL
        UNION ALL
        SELECT proposal.id, {MasterDataCodes.AgentTypes.ProposalNarrative},
            {MasterDataCodes.CommercialResourceTypes.ProposalVersion}, proposal.status_code,
            proposal.agent_provider_code, proposal.agent_model_code,
            NULL::bigint, NULL::integer, proposal.agent_incremental_cost_minor,
            proposal.created_at_utc
        FROM commercial.proposal_versions proposal
        WHERE proposal.tenant_id = {tenantId.Value}
          AND proposal.agent_provider_code IS NOT NULL
        """;
}
