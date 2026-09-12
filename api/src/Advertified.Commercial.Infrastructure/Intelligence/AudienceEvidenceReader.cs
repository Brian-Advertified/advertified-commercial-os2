using System.Text.Json;
using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Application.Intelligence;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Intelligence;

internal static class AudienceEvidenceReader
{
    private static readonly JsonSerializerOptions StoredJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
    internal static async Task<IReadOnlyList<AudienceEvidenceFact>> ReadAsync(
        GovernanceDbContext database, CommercialProblemInput input, CancellationToken cancellationToken)
    {
        var briefResearch = await ReadBriefResearchAsync(database, input, cancellationToken);
        if (input.EvidenceItemIds.Count == 0) return briefResearch;
        var ids = input.EvidenceItemIds.ToArray();
        var rows = await database.Database.SqlQuery<AudienceEvidenceRow>($"""
            SELECT id AS "Id", reviewed_value_json::text AS "Value"
            FROM commercial.evidence_items
            WHERE tenant_id = {input.TenantId} AND id = ANY({ids})
              AND claim_type_code = {MasterDataCodes.EvidenceClaimTypes.CustomerGroup}
              AND review_status_code = {MasterDataCodes.LifecycleStatuses.Approved}
              AND reviewed_by IS NOT NULL AND reviewed_value_json IS NOT NULL
            """).ToArrayAsync(cancellationToken);
        return rows.Select(row => Parse(row.Id, row.Value, input.Audiences))
            .OfType<AudienceEvidenceFact>().Concat(briefResearch).ToArray();
    }

    private static async Task<AudienceEvidenceFact[]> ReadBriefResearchAsync(
        GovernanceDbContext database, CommercialProblemInput input, CancellationToken cancellationToken)
    {
        var json = await database.Database.SqlQuery<string>($"""
            SELECT audience_research_json::text AS "Value" FROM commercial.brief_versions
            WHERE tenant_id = {input.TenantId} AND id = {input.BriefVersionId}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Approved}
              AND approved_by IS NOT NULL
            """).SingleOrDefaultAsync(cancellationToken);
        if (json is null) return [];
        var research = JsonSerializer.Deserialize<BriefAudienceResearch[]>(json, StoredJson) ?? [];
        return research.Select(item => new AudienceEvidenceFact(null, item.AudienceName,
            item.Language, item.LifeStage, item.LsmSem, item.LsmSemTaxonomy,
            item.LsmSemTaxonomyVersion, item.NeedState, item.BuyingContext,
            item.MessageContext, item.MomentContext) { BriefVersionId = input.BriefVersionId }).ToArray();
    }

    internal static AudienceEvidenceFact? Parse(Guid id, string json, IReadOnlyList<string> audiences)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var name = Text(root, "audienceName", 300);
        if (name is null || !audiences.Contains(name, StringComparer.OrdinalIgnoreCase)) return null;
        var segment = Text(root, "lsmSem", 100);
        var taxonomy = Text(root, "lsmSemTaxonomy", 200);
        var version = Text(root, "lsmSemTaxonomyVersion", 100);
        if (segment is not null && (taxonomy is null || version is null)) segment = null;
        return new(id, name, Text(root, "language", 100), Text(root, "lifeStage", 200),
            segment, segment is null ? null : taxonomy, segment is null ? null : version,
            Text(root, "needState", 1000), Text(root, "buyingContext", 500),
            Text(root, "messageContext", 200), Text(root, "momentContext", 200));
    }

    private static string? Text(JsonElement value, string key, int maximum)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(key, out var item) ||
            item.ValueKind != JsonValueKind.String) return null;
        var text = item.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(text) || text.Length > maximum ? null : text;
    }

    private sealed record AudienceEvidenceRow(Guid Id, string Value);
}
