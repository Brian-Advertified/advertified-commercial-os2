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

    internal Task<PlanningBriefRow?> FindBriefAsync(
        TenantId tenantId,
        Guid briefVersionId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<PlanningBriefRow>($"""
            SELECT version.id AS "Id", version.tenant_id AS "TenantId",
                version.brief_id AS "BriefId", brief.owner_user_id AS "OwnerUserId",
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
            WHERE version.tenant_id = {tenantId.Value} AND version.id = {briefVersionId}
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<List<string>> ListAvailableChannelsAsync(
        TenantId tenantId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<string>($"""
            SELECT DISTINCT version.channel_code AS "Value"
            FROM commercial.inventory_products product
            JOIN commercial.inventory_product_versions version
              ON version.tenant_id = product.tenant_id
             AND version.id = product.current_version_id
            JOIN commercial.inventory_rates rate
              ON rate.tenant_id = version.tenant_id
             AND rate.product_version_id = version.id
            WHERE product.tenant_id = {tenantId.Value}
              AND product.status_code = {MasterDataCodes.LifecycleStatuses.Active}
            ORDER BY "Value"
            """).ToListAsync(cancellationToken);

    internal Task<List<PlanningInventoryRow>> ListInventoryAsync(
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var statement = FormattableStringFactory.Create(
            PlanningInventorySelect +
            "\nWHERE product.tenant_id = {0} AND product.status_code = {1}\nORDER BY product.id",
            tenantId.Value, MasterDataCodes.LifecycleStatuses.Active);
        return dbContext.Database.SqlQuery<PlanningInventoryRow>(statement)
            .ToListAsync(cancellationToken);
    }

    internal async Task<PlanningInventoryRow?> FindInventoryAsync(
        TenantId tenantId,
        Guid productId,
        CancellationToken cancellationToken)
    {
        var statement = FormattableStringFactory.Create(
            PlanningInventorySelect +
            "\nWHERE product.tenant_id = {0} AND product.id = {1} AND product.status_code = {2}",
            tenantId.Value, productId, MasterDataCodes.LifecycleStatuses.Active);
        var rows = await dbContext.Database.SqlQuery<PlanningInventoryRow>(statement)
            .ToListAsync(cancellationToken);
        return rows.SingleOrDefault();
    }

    internal Task<List<PlanningSpatialPeerRow>> ListSpatialPeersAsync(
        TenantId tenantId,
        Guid targetProductVersionId,
        decimal maximumRadiusKilometres,
        CancellationToken cancellationToken)
    {
        var maximumRadiusMetres = maximumRadiusKilometres * 1000m;
        return dbContext.Database.SqlQuery<PlanningSpatialPeerRow>($"""
            SELECT peer.id AS "ProductVersionId",
                (ST_Distance(peer.spatial_location, target.spatial_location) / 1000.0)::numeric
                    AS "DistanceKilometres"
            FROM commercial.inventory_product_versions target
            JOIN commercial.inventory_product_versions peer
              ON peer.tenant_id = target.tenant_id AND peer.id <> target.id
            JOIN commercial.inventory_products product
              ON product.tenant_id = peer.tenant_id AND product.current_version_id = peer.id
            WHERE target.tenant_id = {tenantId.Value}
              AND target.id = {targetProductVersionId}
              AND target.spatial_location IS NOT NULL
              AND peer.spatial_location IS NOT NULL
              AND product.status_code = {MasterDataCodes.LifecycleStatuses.Active}
              AND ST_DWithin(
                    peer.spatial_location,
                    target.spatial_location,
                    {maximumRadiusMetres})
            ORDER BY "DistanceKilometres", peer.id
            """).ToListAsync(cancellationToken);
    }

    internal async Task<AudienceSetRow?> FindLatestAudienceAsync(
        TenantId tenantId,
        Guid briefVersionId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Database.SqlQuery<AudienceSetRow>($"""
            SELECT id AS "Id", brief_version_id AS "BriefVersionId",
                version_no AS "VersionNumber", input_hash AS "InputHash",
                status_code AS "Status", created_at_utc AS "CreatedAtUtc"
            FROM commercial.audience_definition_sets
            WHERE tenant_id = {tenantId.Value} AND brief_version_id = {briefVersionId}
            ORDER BY version_no DESC LIMIT 1
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

    private const string PlanningInventorySelect = """
        SELECT product.id AS "ProductId", version.id AS "ProductVersionId",
            product.supplier_id AS "SupplierId", version.name AS "Name",
            version.channel_code AS "Channel", version.product_type_code AS "ProductType",
            version.geography AS "Geography", version.latitude AS "Latitude",
            version.longitude AS "Longitude", rate.id AS "RateId",
            rate.rate_type_code AS "RateType", rate.currency_code AS "Currency",
            rate.amount_minor AS "RateAmountMinor", rate.effective_from AS "EffectiveFrom",
            rate.effective_to AS "EffectiveTo", availability.id AS "AvailabilityId",
            availability.availability_code AS "Availability",
            availability.observed_at_utc AS "ObservedAtUtc",
            availability.valid_until_utc AS "ValidUntilUtc",
            availability.source_locator AS "AvailabilitySource"
        FROM commercial.inventory_products product
        JOIN commercial.inventory_product_versions version
          ON version.tenant_id = product.tenant_id
         AND version.id = product.current_version_id
        LEFT JOIN LATERAL (
            SELECT item.* FROM commercial.inventory_rates item
            WHERE item.tenant_id = version.tenant_id
              AND item.product_version_id = version.id
            ORDER BY item.effective_from DESC NULLS LAST, item.id
            LIMIT 1) rate ON TRUE
        LEFT JOIN LATERAL (
            SELECT item.* FROM commercial.inventory_availability item
            WHERE item.tenant_id = version.tenant_id
              AND item.product_version_id = version.id
            ORDER BY item.observed_at_utc DESC NULLS LAST, item.id
            LIMIT 1) availability ON TRUE
        """;

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
            created_at_utc AS "CreatedAtUtc"
        FROM commercial.media_plan_versions
        """;
}
