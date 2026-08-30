using System.Text.Json;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Opportunity;

internal static class OpportunityEvidenceBatchPersistence
{
    private const int MaximumClaims = 100;
    private static readonly JsonSerializerOptions StoredJson =
        new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> ClaimTypes =
        MasterDataRegistryReader.Read().Collections
            .Single(item => item.Code == MasterDataCodes.EvidenceClaimTypes.Collection).Items
            .Where(item => item.IsActive).Select(item => item.Code)
            .ToHashSet(StringComparer.Ordinal);

    internal static async Task InsertClaimsAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid opportunityId,
        Guid sourceId,
        Guid createdBy,
        Guid reviewer,
        IReadOnlyList<CandidateEvidenceCommand> claims,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var prepared = Prepare(claims);
        var payload = JsonSerializer.Serialize(prepared, StoredJson);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.evidence_items (
                id, tenant_id, opportunity_id, source_id, locator, claim_type_code,
                original_value_json, excerpt, confidence, review_status_code,
                created_by, version, created_at_utc, updated_at_utc)
            SELECT value."id", {tenantId.Value}, {opportunityId}, {sourceId},
                value."locator", value."claimType", value."valueJson"::jsonb,
                value."excerpt", value."confidence",
                {MasterDataCodes.LifecycleStatuses.Pending}, {createdBy}, 1, {now}, {now}
            FROM jsonb_to_recordset({payload}::jsonb) AS value(
                "id" uuid, "taskId" uuid, "locator" text, "claimType" text,
                "valueJson" text, "excerpt" text, "confidence" numeric);

            INSERT INTO commercial.human_tasks (
                id, tenant_id, opportunity_id, task_type_code, status_code, title,
                why_it_matters, resource_type_code, resource_id, resource_version,
                assignee_user_id, action_schema_json, version, created_at_utc)
            SELECT value."taskId", {tenantId.Value}, {opportunityId},
                {MasterDataCodes.HumanTaskTypes.EvidenceItemReview},
                {MasterDataCodes.LifecycleStatuses.Pending},
                {"Review captured evidence"},
                {"Only reviewed source claims can support opportunity recommendations."},
                {MasterDataReferences.CommercialResourceTypes.EvidenceItem.Value},
                value."id", 1, {reviewer}, {"{}"}::jsonb, 1, {now}
            FROM jsonb_to_recordset({payload}::jsonb) AS value(
                "id" uuid, "taskId" uuid, "locator" text, "claimType" text,
                "valueJson" text, "excerpt" text, "confidence" numeric);
            """, cancellationToken);
    }

    private static PreparedEvidenceClaim[] Prepare(
        IReadOnlyList<CandidateEvidenceCommand> claims)
    {
        if (claims.Count is 0 or > MaximumClaims)
        {
            throw new ArgumentException(
                "Supply between one and 100 candidate evidence claims.", nameof(claims));
        }
        return claims.Select(claim =>
        {
            var claimType = OpportunityCommandSupport.Required(
                claim.ClaimType, 100, nameof(claims)).ToUpperInvariant();
            if (!ClaimTypes.Contains(claimType) || claim.Confidence is < 0 or > 1)
            {
                throw new ArgumentException("A candidate evidence claim is invalid.",
                    nameof(claims));
            }
            return new PreparedEvidenceClaim(
                Guid.NewGuid(),
                Guid.NewGuid(),
                OpportunityCommandSupport.Required(claim.Locator, 500, nameof(claims)),
                claimType,
                OpportunityCommandSupport.Json(claim.StructuredValueJson, nameof(claims)),
                OpportunityCommandSupport.Required(claim.Excerpt, 2_000, nameof(claims)),
                claim.Confidence);
        }).ToArray();
    }

    private sealed record PreparedEvidenceClaim(
        Guid Id,
        Guid TaskId,
        string Locator,
        string ClaimType,
        string ValueJson,
        string Excerpt,
        decimal Confidence);
}
