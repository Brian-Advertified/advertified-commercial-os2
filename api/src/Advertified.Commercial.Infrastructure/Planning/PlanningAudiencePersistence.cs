using System.Text.Json;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Planning;

internal static class PlanningAudiencePersistence
{
    private const int MaximumAudiences = 50;
    private static readonly JsonSerializerOptions StoredJson =
        new(JsonSerializerDefaults.Web);

    internal static Task<int> InsertAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid setId,
        IReadOnlyList<PlannedAudienceRecord> audiences,
        CancellationToken cancellationToken)
    {
        if (audiences.Count is 0 or > MaximumAudiences)
        {
            throw new InvalidOperationException(
                "The audience proposal contains an invalid number of segments.");
        }
        var payload = JsonSerializer.Serialize(audiences.Select(item => new AudiencePayload(
            item.Id,
            item.Proposal.Name,
            item.Proposal.Description,
            item.Proposal.NeedState,
            item.Proposal.BuyingContext,
            JsonSerializer.Serialize(item.Proposal.Geographies, StoredJson),
            item.Proposal.Language,
            item.Proposal.LifeStage,
            item.Proposal.LsmSem,
            item.Proposal.Classification,
            JsonSerializer.Serialize(item.Proposal.Exclusions, StoredJson),
            JsonSerializer.Serialize(item.Proposal.EvidenceItemIds, StoredJson),
            item.Proposal.Confidence)), StoredJson);
        return dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.audience_definitions (
                id, tenant_id, audience_set_id, name, description, need_state,
                buying_context, geography_json, language, life_stage, lsm_sem,
                classification_code, exclusions_json, evidence_item_ids_json,
                confidence, status_code)
            SELECT value."id", {tenantId.Value}, {setId}, value."name",
                value."description", value."needState", value."buyingContext",
                value."geographiesJson"::jsonb, value."language", value."lifeStage",
                value."lsmSem", value."classification",
                value."exclusionsJson"::jsonb, value."evidenceItemIdsJson"::jsonb,
                value."confidence", {MasterDataCodes.LifecycleStatuses.Approved}
            FROM jsonb_to_recordset({payload}::jsonb) AS value(
                "id" uuid, "name" text, "description" text, "needState" text,
                "buyingContext" text, "geographiesJson" text, "language" text,
                "lifeStage" text, "lsmSem" text, "classification" text,
                "exclusionsJson" text, "evidenceItemIdsJson" text,
                "confidence" numeric)
            """, cancellationToken);
    }

    private sealed record AudiencePayload(
        Guid Id,
        string Name,
        string Description,
        string NeedState,
        string BuyingContext,
        string GeographiesJson,
        string? Language,
        string? LifeStage,
        string? LsmSem,
        string Classification,
        string ExclusionsJson,
        string EvidenceItemIdsJson,
        decimal Confidence);
}
