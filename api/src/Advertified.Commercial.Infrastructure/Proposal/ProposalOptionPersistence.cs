using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Proposal;

internal static class ProposalOptionPersistence
{
    internal static Task<int> InsertAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid proposalId,
        IReadOnlyList<ProposalOptionSnapshot> options,
        CancellationToken cancellationToken)
    {
        if (options.Count is < 1 or > 3)
        {
            throw new ArgumentException("A proposal must contain between one and three choices.",
                nameof(options));
        }
        var payload = ProposalRecordStore.Write(options.Select(item => new OptionPayload(
            Guid.NewGuid(),
            item.Plan.Id,
            item.Plan.VersionNumber,
            item.Label,
            item.Outcome,
            item.Plan.TotalMinor,
            item.Plan.Currency,
            item.DisplayOrder,
            item.Plan.Signature,
            ProposalRecordStore.Write(item.Plan.Channels),
            ProposalRecordStore.Write(item.Plan.Periods),
            ProposalRecordStore.Write(item.Plan.InventoryNames))).ToArray());
        return dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.proposal_options (
                id, tenant_id, proposal_version_id, plan_version_id, plan_version_no,
                label, outcome, budget_minor, currency_code, display_order, plan_signature,
                channels_json, running_periods_json, inventory_json)
            SELECT value."id", {tenantId.Value}, {proposalId}, value."planVersionId",
                value."planVersionNumber", value."label", value."outcome",
                value."budgetMinor", value."currency", value."displayOrder",
                value."planSignature", value."channelsJson"::jsonb,
                value."runningPeriodsJson"::jsonb, value."inventoryJson"::jsonb
            FROM jsonb_to_recordset({payload}::jsonb) AS value(
                "id" uuid, "planVersionId" uuid, "planVersionNumber" integer,
                "label" text, "outcome" text, "budgetMinor" bigint,
                "currency" text, "displayOrder" integer, "planSignature" text,
                "channelsJson" text, "runningPeriodsJson" text,
                "inventoryJson" text)
            """, cancellationToken);
    }

    internal static Task<int> UpdateAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid proposalId,
        IReadOnlyList<ProposalOptionEdit> edits,
        CancellationToken cancellationToken)
    {
        var payload = ProposalRecordStore.Write(edits.Select(item => new OptionEditPayload(
            item.OptionId, item.Label, item.Outcome)).ToArray());
        return dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.proposal_options option
            SET label = value."label", outcome = value."outcome"
            FROM jsonb_to_recordset({payload}::jsonb) AS value(
                "id" uuid, "label" text, "outcome" text)
            WHERE option.tenant_id = {tenantId.Value}
              AND option.proposal_version_id = {proposalId}
              AND option.id = value."id"
            """, cancellationToken);
    }

    private sealed record OptionPayload(
        Guid Id,
        Guid PlanVersionId,
        int PlanVersionNumber,
        string Label,
        string Outcome,
        long BudgetMinor,
        string Currency,
        int DisplayOrder,
        string PlanSignature,
        string ChannelsJson,
        string RunningPeriodsJson,
        string InventoryJson);

    private sealed record OptionEditPayload(
        Guid Id,
        string Label,
        string Outcome);
}
