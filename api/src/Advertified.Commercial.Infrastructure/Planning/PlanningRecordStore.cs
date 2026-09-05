using System.Runtime.CompilerServices;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Advertified.Commercial.Infrastructure.Planning;

public sealed partial class PlanningRecordStore(GovernanceDbContext dbContext)
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

    internal Task<List<PlanningSummaryRow>> ListPlanningAsync(
        TenantId tenantId,
        Guid actorId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<PlanningSummaryRow>($"""
            SELECT brief.id AS "BriefId", version.id AS "BriefVersionId",
                COALESCE(NULLIF(BTRIM(client.trading_name), ''), client.legal_name)
                    AS "ClientName",
                brief.title AS "BriefTitle",
                (SELECT audience.status_code
                    FROM commercial.audience_definition_sets audience
                    WHERE audience.tenant_id = version.tenant_id
                      AND audience.brief_version_id = version.id
                    ORDER BY audience.version_no DESC
                    LIMIT 1) AS "AudienceStatus",
                (SELECT mix.status_code
                    FROM commercial.media_mix_versions mix
                    WHERE mix.tenant_id = version.tenant_id
                      AND mix.brief_version_id = version.id
                    ORDER BY mix.version_no DESC
                    LIMIT 1) AS "MediaMixStatus",
                (SELECT plan.status_code
                    FROM commercial.media_plan_versions plan
                    WHERE plan.tenant_id = version.tenant_id
                      AND plan.brief_version_id = version.id
                    ORDER BY plan.version_no DESC
                    LIMIT 1) AS "MediaPlanStatus",
                brief.updated_at_utc AS "UpdatedAtUtc"
            FROM commercial.campaign_briefs brief
            JOIN commercial.brief_versions version
              ON version.tenant_id = brief.tenant_id
             AND version.id = COALESCE(brief.approved_version_id, brief.ready_version_id)
            JOIN commercial.client_accounts client
              ON client.tenant_id = brief.tenant_id
             AND client.id = brief.client_account_id
            WHERE brief.tenant_id = {tenantId.Value}
              AND brief.owner_user_id = {actorId}
              AND EXISTS (
                SELECT 1 FROM commercial.audience_definition_sets audience
                WHERE audience.tenant_id = version.tenant_id
                  AND audience.brief_version_id = version.id)
            ORDER BY brief.updated_at_utc DESC, brief.id
            """).ToListAsync(cancellationToken);

    internal Task<PlanningBriefRow?> FindBriefAsync(
        TenantId tenantId,
        Guid briefVersionId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<PlanningBriefRow>($"""
            SELECT version.id AS "Id", version.tenant_id AS "TenantId",
                version.brief_id AS "BriefId",
                COALESCE(NULLIF(BTRIM(client.trading_name), ''), client.legal_name)
                    AS "ClientName",
                brief.owner_user_id AS "OwnerUserId",
                version.status_code AS "Status", version.objective AS "Objective",
                version.audiences_json::text AS "AudiencesJson",
                version.geographies_json::text AS "GeographiesJson",
                version.budget_minor AS "BudgetMinor",
                version.budget_unknown AS "BudgetUnknown",
                version.currency_code AS "Currency", version.vat_status_code AS "VatStatus",
                version.fees_minor AS "FeesMinor",
                COALESCE((SELECT jsonb_agg(binding.evidence_item_id ORDER BY binding.evidence_item_id)
                    FROM commercial.brief_version_evidence_items binding
                    WHERE binding.tenant_id = version.tenant_id
                      AND binding.brief_version_id = version.id), '[]'::jsonb)::text
                    AS "EvidenceIdsJson",
                version.version AS "Version"
            FROM commercial.brief_versions version
            JOIN commercial.campaign_briefs brief
              ON brief.tenant_id = version.tenant_id AND brief.id = version.brief_id
            JOIN commercial.client_accounts client
              ON client.tenant_id = brief.tenant_id
             AND client.id = brief.client_account_id
            WHERE version.tenant_id = {tenantId.Value} AND version.id = {briefVersionId}
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<CampaignModeRow?> FindCampaignModeAsync(
        TenantId tenantId,
        Guid briefVersionId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<CampaignModeRow>($"""
            SELECT id AS "Id", brief_version_id AS "BriefVersionId",
                mode_code AS "Mode", decision_source_code AS "DecisionSource",
                confidence AS "Confidence", reason AS "Reason",
                selected_by AS "SelectedBy", version AS "Version",
                selected_at_utc AS "SelectedAtUtc"
            FROM commercial.campaign_mode_selections
            WHERE tenant_id = {tenantId.Value} AND brief_version_id = {briefVersionId}
            """).SingleOrDefaultAsync(cancellationToken);

    internal async Task LockCampaignBriefAsync(
        TenantId tenantId,
        Guid briefId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Database.SqlQuery<Guid>($"""
            SELECT id AS "Value"
            FROM commercial.campaign_briefs
            WHERE tenant_id = {tenantId.Value} AND id = {briefId}
            FOR UPDATE
            """).ToListAsync(cancellationToken);
        if (rows.Count != 1)
        {
            throw new UnauthorizedAccessException("Campaign Brief access denied.");
        }
    }

    internal Task<List<string>> ListCampaignModesForBriefAsync(
        TenantId tenantId,
        Guid briefId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<string>($"""
            SELECT DISTINCT selection.mode_code AS "Value"
            FROM commercial.campaign_mode_selections selection
            JOIN commercial.brief_versions version
              ON version.tenant_id = selection.tenant_id
             AND version.id = selection.brief_version_id
            WHERE selection.tenant_id = {tenantId.Value}
              AND version.brief_id = {briefId}
            ORDER BY "Value"
            """).ToListAsync(cancellationToken);

    internal Task<bool> HasPlanningArtifactsAsync(
        TenantId tenantId,
        Guid briefVersionId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<bool>($"""
            SELECT (
                EXISTS (SELECT 1 FROM commercial.audience_definition_sets
                    WHERE tenant_id = {tenantId.Value} AND brief_version_id = {briefVersionId})
                OR EXISTS (SELECT 1 FROM commercial.media_mix_versions
                    WHERE tenant_id = {tenantId.Value} AND brief_version_id = {briefVersionId})
                OR EXISTS (SELECT 1 FROM commercial.inventory_shortlist_versions
                    WHERE tenant_id = {tenantId.Value} AND brief_version_id = {briefVersionId})
                OR EXISTS (SELECT 1 FROM commercial.media_plan_versions
                    WHERE tenant_id = {tenantId.Value} AND brief_version_id = {briefVersionId})
            ) AS "Value"
            """).SingleAsync(cancellationToken);

    internal Task<List<string>> ListAvailableChannelsAsync(
        TenantId tenantId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<string>($"""
            SELECT channel AS "Value"
            FROM (
                SELECT version.channel_code AS channel
                FROM commercial.inventory_products product
                JOIN commercial.inventory_product_versions version
                  ON version.tenant_id = product.tenant_id
                 AND version.id = product.current_version_id
                JOIN commercial.inventory_rates rate
                  ON rate.tenant_id = version.tenant_id
                 AND rate.product_version_id = version.id
                WHERE product.tenant_id = {tenantId.Value}
                  AND product.status_code = {MasterDataCodes.LifecycleStatuses.Active}
                UNION
                SELECT snapshot.channel_code AS channel
                FROM commercial.marketplace_listings listing
                JOIN commercial.marketplace_listing_versions snapshot
                  ON snapshot.supplier_tenant_id = listing.supplier_tenant_id
                 AND snapshot.id = listing.current_version_id
                WHERE listing.supplier_tenant_id <> {tenantId.Value}
                  AND listing.status_code =
                    {MasterDataCodes.MarketplaceListingStatuses.Published}
            ) available
            ORDER BY "Value"
            """).ToListAsync(cancellationToken);

    internal async Task<AudienceSetRow?> FindLatestAudienceAsync(
        TenantId tenantId,
        Guid briefVersionId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Database.SqlQuery<AudienceSetRow>($"""
            SELECT id AS "Id", brief_version_id AS "BriefVersionId",
                version_no AS "VersionNumber",
                target_audience_ids_json::text AS "TargetAudienceIdsJson",
                targeting_rationale AS "TargetingRationale",
                positioning_statement AS "PositioningStatement", input_hash AS "InputHash",
                status_code AS "Status", created_at_utc AS "CreatedAtUtc"
            FROM commercial.audience_definition_sets
            WHERE tenant_id = {tenantId.Value} AND brief_version_id = {briefVersionId}
            ORDER BY version_no DESC LIMIT 1
            """).ToListAsync(cancellationToken);
        return rows.SingleOrDefault();
    }

    internal async Task<AudienceSetRow?> FindAudienceAsync(
        TenantId tenantId,
        Guid audienceSetId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Database.SqlQuery<AudienceSetRow>($"""
            SELECT id AS "Id", brief_version_id AS "BriefVersionId",
                version_no AS "VersionNumber",
                target_audience_ids_json::text AS "TargetAudienceIdsJson",
                targeting_rationale AS "TargetingRationale",
                positioning_statement AS "PositioningStatement", input_hash AS "InputHash",
                status_code AS "Status", created_at_utc AS "CreatedAtUtc"
            FROM commercial.audience_definition_sets
            WHERE tenant_id = {tenantId.Value} AND id = {audienceSetId}
            """).ToListAsync(cancellationToken);
        return rows.SingleOrDefault();
    }

    internal async Task<MediaMixRow?> FindLatestMixAsync(
        TenantId tenantId,
        Guid briefVersionId,
        CancellationToken cancellationToken)
    {
        var statement = FormattableStringFactory.Create(
            MediaMixSelect + "\nWHERE tenant_id = {0} AND brief_version_id = {1}\n" +
            "ORDER BY version_no DESC LIMIT 1",
            tenantId.Value, briefVersionId);
        var rows = await dbContext.Database.SqlQuery<MediaMixRow>(statement)
            .ToListAsync(cancellationToken);
        return rows.SingleOrDefault();
    }

    internal async Task<MediaMixRow?> FindMixAsync(
        TenantId tenantId,
        Guid mixVersionId,
        CancellationToken cancellationToken)
    {
        var statement = FormattableStringFactory.Create(
            MediaMixSelect + "\nWHERE tenant_id = {0} AND id = {1}",
            tenantId.Value, mixVersionId);
        var rows = await dbContext.Database.SqlQuery<MediaMixRow>(statement)
            .ToListAsync(cancellationToken);
        return rows.SingleOrDefault();
    }

    internal async Task<ShortlistRow?> FindLatestShortlistAsync(
        TenantId tenantId,
        Guid briefVersionId,
        CancellationToken cancellationToken)
    {
        var statement = FormattableStringFactory.Create(
            ShortlistSelect + "\nWHERE tenant_id = {0} AND brief_version_id = {1}\n" +
            "ORDER BY version_no DESC LIMIT 1",
            tenantId.Value, briefVersionId);
        var rows = await dbContext.Database.SqlQuery<ShortlistRow>(statement)
            .ToListAsync(cancellationToken);
        return rows.SingleOrDefault();
    }

    internal async Task<ShortlistRow?> FindShortlistAsync(
        TenantId tenantId,
        Guid shortlistVersionId,
        CancellationToken cancellationToken)
    {
        var statement = FormattableStringFactory.Create(
            ShortlistSelect + "\nWHERE tenant_id = {0} AND id = {1}",
            tenantId.Value, shortlistVersionId);
        var rows = await dbContext.Database.SqlQuery<ShortlistRow>(statement)
            .ToListAsync(cancellationToken);
        return rows.SingleOrDefault();
    }

    internal async Task<MediaPlanRow?> FindLatestPlanAsync(
        TenantId tenantId,
        Guid briefVersionId,
        CancellationToken cancellationToken)
    {
        var statement = FormattableStringFactory.Create(
            MediaPlanSelect + "\nWHERE tenant_id = {0} AND brief_version_id = {1}\n" +
            "ORDER BY version_no DESC LIMIT 1",
            tenantId.Value, briefVersionId);
        var rows = await dbContext.Database.SqlQuery<MediaPlanRow>(statement)
            .ToListAsync(cancellationToken);
        return rows.SingleOrDefault();
    }

    internal async Task<MediaPlanRow?> FindPlanAsync(
        TenantId tenantId,
        Guid planVersionId,
        CancellationToken cancellationToken)
    {
        var statement = FormattableStringFactory.Create(
            MediaPlanSelect + "\nWHERE tenant_id = {0} AND id = {1}",
            tenantId.Value, planVersionId);
        var rows = await dbContext.Database.SqlQuery<MediaPlanRow>(statement)
            .ToListAsync(cancellationToken);
        return rows.SingleOrDefault();
    }

    internal Task<List<MediaPlanRow>> ListApprovedPlansAsync(
        TenantId tenantId,
        Guid briefVersionId,
        CancellationToken cancellationToken)
    {
        var statement = FormattableStringFactory.Create(
            MediaPlanSelect + "\nWHERE tenant_id = {0} AND brief_version_id = {1} " +
            "AND status_code = {2}\nORDER BY version_no DESC",
            tenantId.Value, briefVersionId, MasterDataCodes.LifecycleStatuses.Approved);
        return dbContext.Database.SqlQuery<MediaPlanRow>(statement).ToListAsync(cancellationToken);
    }

    internal Task<List<MediaPlanRow>> ListPlansAsync(
        TenantId tenantId,
        Guid[] planIds,
        CancellationToken cancellationToken)
    {
        if (planIds.Length == 0) return Task.FromResult(new List<MediaPlanRow>());
        var statement = FormattableStringFactory.Create(
            MediaPlanSelect + "\nWHERE tenant_id = {0} AND id = ANY({1})",
            tenantId.Value, planIds);
        return dbContext.Database.SqlQuery<MediaPlanRow>(statement)
            .ToListAsync(cancellationToken);
    }

    private const string MediaMixSelect = """
        SELECT id AS "Id", brief_version_id AS "BriefVersionId",
            audience_set_id AS "AudienceSetId", version_no AS "VersionNumber",
            total_budget_minor AS "TotalBudgetMinor", currency_code AS "Currency",
            allocations_json::text AS "AllocationsJson",
            assumptions_json::text AS "AssumptionsJson", input_hash AS "InputHash",
            status_code AS "Status", created_by AS "CreatedBy", approved_by AS "ApprovedBy",
            version AS "Version", created_at_utc AS "CreatedAtUtc"
        FROM commercial.media_mix_versions
        """;

    private const string ShortlistSelect = """
        SELECT id AS "Id", brief_version_id AS "BriefVersionId",
            mix_version_id AS "MixVersionId", version_no AS "VersionNumber",
            input_hash AS "InputHash", status_code AS "Status",
            assumptions_json::text AS "AssumptionsJson", version AS "Version",
            created_at_utc AS "CreatedAtUtc"
        FROM commercial.inventory_shortlist_versions
        """;

    private const string MediaPlanSelect = """
        SELECT id AS "Id", brief_version_id AS "BriefVersionId",
            mix_version_id AS "MixVersionId", shortlist_version_id AS "ShortlistVersionId",
            version_no AS "VersionNumber", subtotal_minor AS "SubtotalMinor",
            fees_minor AS "FeesMinor", vat_minor AS "VatMinor", total_minor AS "TotalMinor",
            currency_code AS "Currency", supply_confidence_code AS "SupplyConfidence",
            input_hash AS "InputHash", status_code AS "Status",
            assumptions_json::text AS "AssumptionsJson",
            critic_report_json::text AS "CriticReportJson", created_by AS "CreatedBy",
            approved_by AS "ApprovedBy", version AS "Version",
            created_at_utc AS "CreatedAtUtc",
            commercial_policy_version_id AS "CommercialPolicyVersionId"
        FROM commercial.media_plan_versions
        """;
}
