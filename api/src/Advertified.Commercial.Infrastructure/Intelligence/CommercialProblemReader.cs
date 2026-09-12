using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Advertified.Commercial.Application.Intelligence;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Intelligence;

internal sealed record CommercialProblemSnapshot(
    Guid BriefVersionId,
    Guid BriefId,
    Guid OwnerUserId,
    string Status,
    string ClientName,
    string BusinessProblem,
    string Objective,
    IReadOnlyList<string> Audiences,
    IReadOnlyList<string> Geographies,
    IReadOnlyList<string> MediaRequirements,
    IReadOnlyList<string> Constraints,
    IReadOnlyList<CommercialProblemConflictInput> Conflicts,
    IReadOnlyList<string> SuccessMeasures,
    long? BudgetMinor,
    bool BudgetUnknown,
    string? Currency,
    IReadOnlyList<Guid> EvidenceItemIds,
    long Version)
{
    private static readonly JsonSerializerOptions CanonicalJson = new(JsonSerializerDefaults.Web);

    internal CommercialProblemInput ToInput(
        TenantId tenantId,
        ActorId actorId,
        Guid runId,
        Guid correlationId) => new(
            tenantId.Value,
            actorId.Value,
            runId,
            correlationId,
            BriefVersionId,
            Version,
            ClientName,
            BusinessProblem,
            Objective,
            Audiences,
            Geographies,
            MediaRequirements,
            Constraints,
            Conflicts,
            SuccessMeasures,
            BudgetUnknown ? null : BudgetMinor,
            Currency,
            EvidenceItemIds);

    internal string InputHash()
    {
        var payload = JsonSerializer.Serialize(new
        {
            BriefVersionId,
            Version,
            ClientName,
            BusinessProblem,
            Objective,
            Audiences,
            Geographies,
            MediaRequirements,
            Constraints,
            Conflicts,
            SuccessMeasures,
            BudgetMinor = BudgetUnknown ? null : BudgetMinor,
            Currency,
            EvidenceItemIds,
        }, CanonicalJson);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))
            .ToLowerInvariant();
    }
}

internal static class CommercialProblemReader
{
    private static readonly JsonSerializerOptions StoredJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    internal static async Task<CommercialProblemSnapshot?> ReadAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid briefVersionId,
        CancellationToken cancellationToken)
    {
        var row = await dbContext.Database.SqlQuery<CommercialProblemRow>($"""
            SELECT version.id AS "BriefVersionId", version.brief_id AS "BriefId",
                brief.owner_user_id AS "OwnerUserId", version.status_code AS "Status",
                COALESCE(NULLIF(BTRIM(client.trading_name), ''), client.legal_name) AS "ClientName",
                version.business_problem AS "BusinessProblem", version.objective AS "Objective",
                version.audiences_json::text AS "AudiencesJson",
                version.geographies_json::text AS "GeographiesJson",
                version.media_requirements_json::text AS "MediaRequirementsJson",
                version.constraints_json::text AS "ConstraintsJson",
                version.conflicts_json::text AS "ConflictsJson",
                version.measurement_json::text AS "MeasurementJson",
                version.budget_minor AS "BudgetMinor", version.budget_unknown AS "BudgetUnknown",
                version.currency_code AS "Currency",
                COALESCE((SELECT jsonb_agg(binding.evidence_item_id ORDER BY binding.evidence_item_id)
                    FROM commercial.brief_version_evidence_items binding
                    WHERE binding.tenant_id = version.tenant_id
                      AND binding.brief_version_id = version.id), '[]'::jsonb)::text AS "EvidenceIdsJson",
                version.version AS "Version"
            FROM commercial.brief_versions version
            JOIN commercial.campaign_briefs brief
              ON brief.tenant_id = version.tenant_id AND brief.id = version.brief_id
            JOIN commercial.client_accounts client
              ON client.tenant_id = brief.tenant_id AND client.id = brief.client_account_id
            WHERE version.tenant_id = {tenantId.Value} AND version.id = {briefVersionId}
            """).SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : new CommercialProblemSnapshot(
            row.BriefVersionId,
            row.BriefId,
            row.OwnerUserId,
            row.Status,
            row.ClientName,
            row.BusinessProblem,
            row.Objective,
            Read<string>(row.AudiencesJson),
            Read<string>(row.GeographiesJson),
            Read<string>(row.MediaRequirementsJson),
            Read<string>(row.ConstraintsJson),
            Read<CommercialProblemConflictInput>(row.ConflictsJson),
            Read<string>(row.MeasurementJson),
            row.BudgetMinor,
            row.BudgetUnknown,
            row.Currency,
            Read<Guid>(row.EvidenceIdsJson),
            row.Version);
    }

    internal static async Task<IReadOnlyList<AgentEvidenceInput>> ReadApprovedEvidenceAsync(
        GovernanceDbContext database,
        CommercialProblemInput input,
        CancellationToken cancellationToken)
    {
        // The bounded operation contract admits at most 100 complete evidence snapshots.
        // Oversize or missing evidence is a blocker, never a silently truncated packet.
        var ids = input.EvidenceItemIds.Distinct().Order().ToArray();
        if (ids.Length == 0) return [];
        if (ids.Length > MarketIntelligenceValidator.MaximumEvidenceSnapshots)
            throw new InvalidOperationException("The approved evidence exceeds this operation's bounded input contract.");
        var rows = await database.Database.SqlQuery<CommercialEvidenceRow>($"""
            SELECT item.id AS "Id", item.claim_type_code AS "ClaimType",
                item.reviewed_value_json::text AS "StructuredValueJson", item.excerpt AS "Excerpt"
            FROM commercial.evidence_items item
            JOIN commercial.brief_version_evidence_items binding
              ON binding.tenant_id = item.tenant_id AND binding.evidence_item_id = item.id
            WHERE item.tenant_id = {input.TenantId} AND binding.brief_version_id = {input.BriefVersionId}
              AND item.id = ANY({ids})
              AND item.review_status_code = {MasterDataCodes.LifecycleStatuses.Approved}
              AND item.reviewed_by IS NOT NULL AND item.reviewed_value_json IS NOT NULL
            ORDER BY item.id
            """).ToArrayAsync(cancellationToken);
        if (rows.Length != ids.Length)
            throw new InvalidOperationException("Required approved Brief evidence is unavailable or no longer reviewed.");
        return rows.Select(ToEvidenceInput).ToArray();
    }

    private static AgentEvidenceInput ToEvidenceInput(CommercialEvidenceRow row)
    {
        using var document = JsonDocument.Parse(row.StructuredValueJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            string.IsNullOrWhiteSpace(row.Excerpt) || row.Excerpt.Length > 2_000)
            throw new InvalidOperationException("Approved evidence does not satisfy the bounded intelligence contract.");
        return new(row.Id, row.ClaimType, document.RootElement.Clone(), row.Excerpt);
    }

    private sealed record CommercialEvidenceRow(
        Guid Id, string ClaimType, string StructuredValueJson, string Excerpt);

    private static T[] Read<T>(string json) =>
        JsonSerializer.Deserialize<T[]>(json, StoredJson) ?? [];

    private sealed record CommercialProblemRow(
        Guid BriefVersionId,
        Guid BriefId,
        Guid OwnerUserId,
        string Status,
        string ClientName,
        string BusinessProblem,
        string Objective,
        string AudiencesJson,
        string GeographiesJson,
        string MediaRequirementsJson,
        string ConstraintsJson,
        string ConflictsJson,
        string MeasurementJson,
        long? BudgetMinor,
        bool BudgetUnknown,
        string? Currency,
        string EvidenceIdsJson,
        long Version);
}
