using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Advertified.Commercial.Infrastructure.Brief;

public sealed class BriefRecordStore(GovernanceDbContext dbContext)
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

    internal Task<CampaignBriefRow?> FindBriefAsync(
        TenantId tenantId,
        Guid briefId,
        CancellationToken cancellationToken) => FindBriefAsync(
            tenantId, briefId, forUpdate: false, cancellationToken);

    internal Task<CampaignBriefRow?> FindBriefForUpdateAsync(
        TenantId tenantId,
        Guid briefId,
        CancellationToken cancellationToken) => FindBriefAsync(
            tenantId, briefId, forUpdate: true, cancellationToken);

    private Task<CampaignBriefRow?> FindBriefAsync(
        TenantId tenantId,
        Guid briefId,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        FormattableString query = forUpdate
            ? (FormattableString)$"""
                SELECT brief.id AS "Id", brief.tenant_id AS "TenantId",
                    brief.client_account_id AS "ClientId",
                    COALESCE(NULLIF(BTRIM(client.trading_name), ''), client.legal_name)
                        AS "ClientName",
                    brief.opportunity_id AS "OpportunityId", brief.title AS "Title",
                    brief.owner_user_id AS "OwnerUserId", brief.status_code AS "Status",
                    brief.current_draft_version_id AS "CurrentDraftVersionId",
                    brief.ready_version_id AS "ReadyVersionId",
                    brief.approved_version_id AS "ApprovedVersionId", brief.version AS "Version",
                    brief.updated_at_utc AS "UpdatedAtUtc"
                FROM commercial.campaign_briefs brief
                JOIN commercial.client_accounts client
                  ON client.tenant_id = brief.tenant_id
                 AND client.id = brief.client_account_id
                WHERE brief.tenant_id = {tenantId.Value} AND brief.id = {briefId}
                FOR UPDATE OF brief
                """
            : (FormattableString)$"""
                SELECT brief.id AS "Id", brief.tenant_id AS "TenantId",
                    brief.client_account_id AS "ClientId",
                    COALESCE(NULLIF(BTRIM(client.trading_name), ''), client.legal_name)
                        AS "ClientName",
                    brief.opportunity_id AS "OpportunityId", brief.title AS "Title",
                    brief.owner_user_id AS "OwnerUserId", brief.status_code AS "Status",
                    brief.current_draft_version_id AS "CurrentDraftVersionId",
                    brief.ready_version_id AS "ReadyVersionId",
                    brief.approved_version_id AS "ApprovedVersionId", brief.version AS "Version",
                    brief.updated_at_utc AS "UpdatedAtUtc"
                FROM commercial.campaign_briefs brief
                JOIN commercial.client_accounts client
                  ON client.tenant_id = brief.tenant_id
                 AND client.id = brief.client_account_id
                WHERE brief.tenant_id = {tenantId.Value} AND brief.id = {briefId}
                """;
        return dbContext.Database.SqlQuery<CampaignBriefRow>(query)
            .SingleOrDefaultAsync(cancellationToken);
    }

    internal Task<BriefVersionRow?> FindVersionAsync(
        TenantId tenantId,
        Guid versionId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQueryRaw<BriefVersionRow>(
                $"{VersionSelect} WHERE version.tenant_id = {{0}} AND version.id = {{1}} GROUP BY version.id",
                tenantId.Value,
                versionId)
            .SingleOrDefaultAsync(cancellationToken);

    internal Task<List<BriefVersionRow>> ListVersionsAsync(
        TenantId tenantId,
        Guid briefId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQueryRaw<BriefVersionRow>(
            $"{VersionSelect} WHERE version.tenant_id = {{0}} AND version.brief_id = {{1}} " +
            "GROUP BY version.id ORDER BY version.version_no",
            tenantId.Value,
            briefId).ToListAsync(cancellationToken);

    internal Task<List<BriefSourceRow>> ListSourcesAsync(
        TenantId tenantId,
        Guid briefId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<BriefSourceRow>($"""
            SELECT id AS "Id", source_type_code AS "SourceType", locator AS "Locator",
                title AS "Title", content AS "Content", content_hash AS "ContentHash",
                created_by AS "CreatedBy", created_at_utc AS "CreatedAtUtc"
            FROM commercial.brief_sources
            WHERE tenant_id = {tenantId.Value} AND brief_id = {briefId}
            ORDER BY created_at_utc, id
            """).ToListAsync(cancellationToken);

    internal Task<BriefSourceRow?> FindFirstSourceAsync(
        TenantId tenantId,
        Guid briefId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<BriefSourceRow>($"""
            SELECT id AS "Id", source_type_code AS "SourceType", locator AS "Locator",
                title AS "Title", content AS "Content", content_hash AS "ContentHash",
                created_by AS "CreatedBy", created_at_utc AS "CreatedAtUtc"
            FROM commercial.brief_sources
            WHERE tenant_id = {tenantId.Value} AND brief_id = {briefId}
            ORDER BY created_at_utc, id LIMIT 1
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<bool> CanAccessAsync(
        TenantId tenantId,
        Guid actorId,
        Guid briefId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1 FROM commercial.campaign_briefs brief
                WHERE brief.tenant_id = {tenantId.Value} AND brief.id = {briefId}
                  AND (
                    brief.owner_user_id = {actorId}
                    OR EXISTS (
                        SELECT 1 FROM commercial.client_account_assignments assignment
                        WHERE assignment.tenant_id = brief.tenant_id
                          AND assignment.client_account_id = brief.client_account_id
                          AND assignment.user_id = {actorId}
                          AND assignment.effective_from_utc <= now()
                          AND (assignment.effective_to_utc IS NULL
                            OR assignment.effective_to_utc > now()))
                    OR EXISTS (
                        SELECT 1 FROM commercial.human_tasks task
                        JOIN commercial.brief_versions candidate
                          ON candidate.tenant_id = task.tenant_id
                         AND candidate.id = task.resource_id
                        WHERE task.tenant_id = brief.tenant_id
                          AND candidate.brief_id = brief.id
                          AND task.assignee_user_id = {actorId}))) AS "Value"
            """).SingleAsync(cancellationToken);

    private const string VersionSelect = """
        SELECT version.id AS "Id", version.brief_id AS "BriefId",
            version.base_version_id AS "BaseVersionId", version.source_id AS "SourceId",
            version.version_no AS "VersionNumber", version.business_problem AS "BusinessProblem",
            version.objective AS "Objective", version.audiences_json::text AS "AudiencesJson",
            version.geographies_json::text AS "GeographiesJson", version.timing AS "Timing",
            version.budget_minor AS "BudgetMinor", version.budget_unknown AS "BudgetUnknown",
            version.currency_code AS "Currency", version.vat_status_code AS "VatStatus",
            version.fees_minor AS "FeesMinor", version.constraints_json::text AS "ConstraintsJson",
            version.measurement_json::text AS "MeasurementJson",
            version.facts_json::text AS "FactsJson", version.unknowns_json::text AS "UnknownsJson",
            version.assumptions_json::text AS "AssumptionsJson",
            version.conflicts_json::text AS "ConflictsJson",
            COALESCE(array_agg(link.evidence_item_id)
                FILTER (WHERE link.evidence_item_id IS NOT NULL), ARRAY[]::uuid[])
                AS "EvidenceItemIds",
            version.status_code AS "Status", version.created_by AS "CreatedBy",
            version.submitted_by AS "SubmittedBy", version.approved_by AS "ApprovedBy",
            version.rejected_by AS "RejectedBy", version.rejection_reason AS "RejectionReason",
            version.requested_changes AS "RequestedChanges", version.version AS "Version",
            version.created_at_utc AS "CreatedAtUtc"
        FROM commercial.brief_versions version
        LEFT JOIN commercial.brief_version_evidence_items link
          ON link.tenant_id = version.tenant_id AND link.brief_version_id = version.id
        """;
}
