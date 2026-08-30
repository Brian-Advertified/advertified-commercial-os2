using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Brief;

internal static class BriefPersistence
{
    internal static Task<int> InsertAggregateAndSourceAsync(
        GovernanceDbContext dbContext,
        BriefAggregateWrite aggregate,
        BriefSourceWrite source,
        CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.campaign_briefs (
                id, tenant_id, client_account_id, opportunity_id, title,
                owner_user_id, status_code, current_draft_version_id,
                approved_version_id, version, created_at_utc, updated_at_utc)
            VALUES (
                {aggregate.Id}, {aggregate.TenantId.Value}, {aggregate.ClientAccountId},
                {aggregate.OpportunityId}, {aggregate.Title}, {aggregate.OwnerUserId},
                {aggregate.Status}, NULL, NULL, {aggregate.Version},
                {aggregate.CreatedAtUtc}, {aggregate.CreatedAtUtc});
            INSERT INTO commercial.brief_sources (
                id, tenant_id, brief_id, source_type_code, locator, title,
                content, content_hash, created_by, created_at_utc)
            VALUES (
                {source.Id}, {aggregate.TenantId.Value}, {aggregate.Id},
                {source.SourceType}, {source.Locator}, {source.Title}, {source.Content},
                {source.ContentHash}, {source.CreatedBy}, {source.CreatedAtUtc});
            """, cancellationToken);

    internal static Task<int> InsertVersionAsync(
        GovernanceDbContext dbContext,
        BriefVersionWrite write,
        CancellationToken cancellationToken)
    {
        var command = write.Command;
        var value = write.Value;
        return dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.brief_versions (
                id, tenant_id, brief_id, base_version_id, source_id, version_no,
                business_problem, objective, audiences_json, geographies_json, timing,
                budget_minor, budget_unknown, currency_code, vat_status_code, fees_minor,
                constraints_json, measurement_json, facts_json, unknowns_json,
                assumptions_json, conflicts_json, evidence_bindings_json, status_code,
                created_by, version, created_at_utc)
            VALUES (
                {write.Id}, {write.TenantId.Value}, {write.BriefId}, {write.BaseVersionId},
                {write.SourceId}, {write.VersionNumber}, {value.BusinessProblem},
                {value.Objective}, {BriefCommandSupport.Json(value.Audiences)}::jsonb,
                {BriefCommandSupport.Json(value.Geographies)}::jsonb, {value.Timing},
                {command.BudgetMinor}, {command.BudgetUnknown}, {value.Currency},
                {value.VatStatus}, {command.FeesMinor},
                {BriefCommandSupport.Json(value.Constraints)}::jsonb,
                {BriefCommandSupport.Json(value.Measurement)}::jsonb,
                {BriefCommandSupport.Json(value.Facts)}::jsonb,
                {BriefCommandSupport.Json(value.Unknowns)}::jsonb,
                {BriefCommandSupport.Json(value.Assumptions)}::jsonb,
                {BriefCommandSupport.Json(value.Conflicts)}::jsonb,
                {write.EvidenceBindingsJson}::jsonb, {write.Status}, {write.CreatedBy},
                {write.Version}, {write.CreatedAtUtc})
            """, cancellationToken);
    }

    internal static Task<int> BindEvidenceAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid briefVersionId,
        IReadOnlyCollection<Guid> evidenceItemIds,
        CancellationToken cancellationToken)
    {
        if (evidenceItemIds.Count == 0)
        {
            return Task.FromResult(0);
        }
        var ids = evidenceItemIds.Distinct().ToArray();
        return dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.brief_version_evidence_items (
                tenant_id, brief_version_id, evidence_item_id)
            SELECT {tenantId.Value}, {briefVersionId}, evidence_item_id
            FROM unnest({ids}) AS evidence_item_id
            ON CONFLICT DO NOTHING
            """, cancellationToken);
    }

    internal static async Task SetCurrentDraftAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid briefId,
        Guid briefVersionId,
        long expectedVersion,
        string status,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.campaign_briefs
            SET status_code = {status}, current_draft_version_id = {briefVersionId},
                version = version + 1, updated_at_utc = {now}
            WHERE tenant_id = {tenantId.Value} AND id = {briefId}
              AND version = {expectedVersion}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
    }
}

internal sealed record BriefAggregateWrite(
    Guid Id,
    TenantId TenantId,
    Guid ClientAccountId,
    Guid? OpportunityId,
    string Title,
    Guid OwnerUserId,
    string Status,
    long Version,
    DateTimeOffset CreatedAtUtc);

internal sealed record BriefSourceWrite(
    Guid Id,
    string SourceType,
    string Locator,
    string Title,
    string Content,
    string ContentHash,
    Guid CreatedBy,
    DateTimeOffset CreatedAtUtc);

internal sealed record BriefVersionWrite(
    Guid Id,
    TenantId TenantId,
    Guid BriefId,
    Guid? BaseVersionId,
    Guid SourceId,
    int VersionNumber,
    CreateBriefVersionCommand Command,
    ValidatedBriefVersion Value,
    string EvidenceBindingsJson,
    string Status,
    Guid CreatedBy,
    long Version,
    DateTimeOffset CreatedAtUtc);
