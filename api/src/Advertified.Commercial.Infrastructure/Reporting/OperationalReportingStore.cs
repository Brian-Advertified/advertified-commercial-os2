using Advertified.Commercial.Application.Reporting;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Reporting;

public sealed class OperationalReportingStore(GovernanceDbContext dbContext)
{
    internal async Task<OperationalReportingData> ReadAsync(
        ActorId actorId,
        TenantId tenantId,
        ReportingQuery query,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await ApplicationDatabaseSession.SetAsync(
            dbContext, new UserId(actorId.Value), tenantId, cancellationToken);
        var metrics = await ReadMetricsAsync(tenantId, query, cancellationToken);
        var channels = await ReadChannelsAsync(tenantId, query, cancellationToken);
        var commercial = await ReadCommercialAsync(tenantId, query, cancellationToken);
        var statuses = await ReadStatusesAsync(tenantId, query, cancellationToken);
        var exceptions = await ReadExceptionsAsync(tenantId, query, cancellationToken);
        var dimensions = await ReadDimensionsAsync(tenantId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(metrics, channels, commercial, statuses, exceptions, dimensions);
    }

    private Task<ReportingMetricRow> ReadMetricsAsync(
        TenantId tenantId,
        ReportingQuery query,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<ReportingMetricRow>($"""
            SELECT
              (SELECT count(*)::int FROM commercial.opportunities item
                WHERE item.tenant_id = {tenantId.Value}
                  AND ({query.From}::date IS NULL OR item.created_at_utc >= {query.From}::date)
                  AND ({query.To}::date IS NULL OR item.created_at_utc < ({query.To}::date + 1))
                  AND ({query.ClientAccountId}::uuid IS NULL OR item.client_account_id = {query.ClientAccountId})
                  AND ({query.OwnerUserId}::uuid IS NULL OR item.owner_user_id = {query.OwnerUserId})
                  AND ({query.Status}::text IS NULL OR item.stage_code = {query.Status})) AS "Opportunities",
              (SELECT count(*)::int FROM commercial.campaign_briefs item
                WHERE item.tenant_id = {tenantId.Value}
                  AND ({query.From}::date IS NULL OR item.created_at_utc >= {query.From}::date)
                  AND ({query.To}::date IS NULL OR item.created_at_utc < ({query.To}::date + 1))
                  AND ({query.ClientAccountId}::uuid IS NULL OR item.client_account_id = {query.ClientAccountId})
                  AND ({query.OwnerUserId}::uuid IS NULL OR item.owner_user_id = {query.OwnerUserId})
                  AND ({query.Status}::text IS NULL OR item.status_code = {query.Status})) AS "Briefs",
              (SELECT count(*)::int FROM commercial.audience_definition_sets item
                JOIN commercial.brief_versions version ON version.tenant_id = item.tenant_id
                  AND version.id = item.brief_version_id
                JOIN commercial.campaign_briefs brief ON brief.tenant_id = version.tenant_id
                  AND brief.id = version.brief_id
                WHERE item.tenant_id = {tenantId.Value}
                  AND item.status_code = {MasterDataCodes.LifecycleStatuses.Approved}
                  AND ({query.ClientAccountId}::uuid IS NULL OR brief.client_account_id = {query.ClientAccountId})) AS "ApprovedAudiences",
              (SELECT count(*)::int FROM commercial.human_tasks item
                WHERE item.tenant_id = {tenantId.Value}
                  AND item.status_code = {MasterDataCodes.LifecycleStatuses.Pending}
                  AND ({query.OwnerUserId}::uuid IS NULL OR item.assignee_user_id = {query.OwnerUserId})) AS "PendingApprovals",
              (SELECT count(*)::int FROM commercial.media_plan_versions item
                JOIN commercial.brief_versions version ON version.tenant_id = item.tenant_id
                  AND version.id = item.brief_version_id
                JOIN commercial.campaign_briefs brief ON brief.tenant_id = version.tenant_id
                  AND brief.id = version.brief_id
                WHERE item.tenant_id = {tenantId.Value}
                  AND item.status_code = {MasterDataCodes.LifecycleStatuses.Approved}
                  AND ({query.ClientAccountId}::uuid IS NULL OR brief.client_account_id = {query.ClientAccountId})
                  AND ({query.ReviewerUserId}::uuid IS NULL OR item.approved_by = {query.ReviewerUserId})) AS "ApprovedPlans",
              (SELECT count(*)::int FROM commercial.inventory_shortlist_candidates item
                JOIN commercial.inventory_shortlist_versions shortlist
                  ON shortlist.tenant_id = item.tenant_id AND shortlist.id = item.shortlist_version_id
                JOIN commercial.brief_versions version ON version.tenant_id = shortlist.tenant_id
                  AND version.id = shortlist.brief_version_id
                JOIN commercial.campaign_briefs brief ON brief.tenant_id = version.tenant_id
                  AND brief.id = version.brief_id
                WHERE item.tenant_id = {tenantId.Value} AND item.is_eligible
                  AND ({query.ClientAccountId}::uuid IS NULL OR brief.client_account_id = {query.ClientAccountId})
                  AND ({query.Channel}::text IS NULL OR item.channel_code = {query.Channel})) AS "InventoryRecommendations",
              (SELECT count(*)::int FROM commercial.media_plan_lines item
                JOIN commercial.media_plan_versions plan ON plan.tenant_id = item.tenant_id
                  AND plan.id = item.plan_version_id
                  AND plan.status_code = {MasterDataCodes.LifecycleStatuses.Approved}
                WHERE item.tenant_id = {tenantId.Value}
                  AND ({query.Channel}::text IS NULL OR item.channel_code = {query.Channel})) AS "SelectedInventory",
              (SELECT count(*)::int FROM commercial.marketplace_listings item
                JOIN commercial.marketplace_listing_versions version
                  ON version.supplier_tenant_id = item.supplier_tenant_id
                  AND version.id = item.current_version_id
                WHERE item.status_code = {MasterDataCodes.MarketplaceListingStatuses.Published}
                  AND ({query.Channel}::text IS NULL OR version.channel_code = {query.Channel})
                  AND ({query.SupplierId}::uuid IS NULL OR version.supplier_id = {query.SupplierId})) AS "PublishedListings",
              (SELECT count(*)::int FROM commercial.proposal_versions item
                JOIN commercial.campaign_briefs brief ON brief.tenant_id = item.tenant_id
                  AND brief.id = item.brief_id
                WHERE item.tenant_id = {tenantId.Value}
                  AND ({query.ClientAccountId}::uuid IS NULL OR brief.client_account_id = {query.ClientAccountId})
                  AND ({query.Status}::text IS NULL OR item.status_code = {query.Status})
                  AND ({query.ReviewerUserId}::uuid IS NULL OR item.approved_by = {query.ReviewerUserId})) AS "Proposals",
              (SELECT count(*)::int FROM commercial.bookings item
                WHERE item.buyer_tenant_id = {tenantId.Value}
                  AND ({query.Channel}::text IS NULL OR item.channel_code = {query.Channel})
                  AND ({query.SupplierId}::uuid IS NULL OR item.supplier_id = {query.SupplierId})
                  AND ({query.Status}::text IS NULL OR item.status_code = {query.Status})) AS "Bookings",
              (SELECT count(*)::int FROM commercial.campaigns item
                JOIN commercial.campaign_briefs brief ON brief.tenant_id = item.tenant_id
                  AND brief.id = item.brief_id
                WHERE item.tenant_id = {tenantId.Value}
                  AND ({query.CampaignId}::uuid IS NULL OR item.id = {query.CampaignId})
                  AND ({query.ClientAccountId}::uuid IS NULL OR brief.client_account_id = {query.ClientAccountId})
                  AND ({query.Status}::text IS NULL OR item.status_code = {query.Status})
                  AND ({query.OwnerUserId}::uuid IS NULL OR item.owner_user_id = {query.OwnerUserId})) AS "Campaigns",
              (SELECT count(*)::int FROM commercial.delivery_proofs item
                WHERE item.buyer_tenant_id = {tenantId.Value}
                  AND ({query.CampaignId}::uuid IS NULL OR item.campaign_id = {query.CampaignId})
                  AND ({query.Status}::text IS NULL OR item.status_code = {query.Status})) AS "DeliveryEvidenceItems",
              (SELECT count(*)::int FROM commercial.measurement_report_versions item
                WHERE item.tenant_id = {tenantId.Value}
                  AND ({query.CampaignId}::uuid IS NULL OR item.campaign_id = {query.CampaignId})
                  AND ({query.Status}::text IS NULL OR item.status_code = {query.Status})) AS "MeasurementReports"
            """).SingleAsync(cancellationToken);

    private Task<List<ReportingChannelSpendRow>> ReadChannelsAsync(
        TenantId tenantId,
        ReportingQuery query,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<ReportingChannelSpendRow>($"""
            SELECT line.channel_code AS "Channel", count(DISTINCT plan.id)::int AS "PlanCount",
                COALESCE(sum(line.supplier_cost_minor), 0)::bigint AS "SupplierCostMinor",
                COALESCE(sum(line.client_price_minor), 0)::bigint AS "ClientPriceMinor",
                COALESCE(sum(line.fees_minor), 0)::bigint AS "FeesMinor",
                COALESCE(sum(line.vat_minor), 0)::bigint AS "VatMinor",
                plan.currency_code AS "Currency"
            FROM commercial.media_plan_lines line
            JOIN commercial.media_plan_versions plan ON plan.tenant_id = line.tenant_id
              AND plan.id = line.plan_version_id
              AND plan.status_code = {MasterDataCodes.LifecycleStatuses.Approved}
            JOIN commercial.brief_versions version ON version.tenant_id = plan.tenant_id
              AND version.id = plan.brief_version_id
            JOIN commercial.campaign_briefs brief ON brief.tenant_id = version.tenant_id
              AND brief.id = version.brief_id
            WHERE line.tenant_id = {tenantId.Value}
              AND ({query.From}::date IS NULL OR plan.created_at_utc >= {query.From}::date)
              AND ({query.To}::date IS NULL OR plan.created_at_utc < ({query.To}::date + 1))
              AND ({query.ClientAccountId}::uuid IS NULL OR brief.client_account_id = {query.ClientAccountId})
              AND ({query.Channel}::text IS NULL OR line.channel_code = {query.Channel})
              AND ({query.ReviewerUserId}::uuid IS NULL OR plan.approved_by = {query.ReviewerUserId})
            GROUP BY line.channel_code, plan.currency_code
            ORDER BY line.channel_code, plan.currency_code
            """).ToListAsync(cancellationToken);

    private Task<ReportingCommercialRow> ReadCommercialAsync(
        TenantId tenantId,
        ReportingQuery query,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<ReportingCommercialRow>($"""
            SELECT count(*)::int AS "BookingCount",
                COALESCE(sum(booking.supplier_cost_minor), 0)::bigint AS "SupplierCostMinor",
                COALESCE(sum(booking.markup_minor), 0)::bigint AS "MarkupMinor",
                COALESCE(sum(booking.commission_minor), 0)::bigint AS "CommissionMinor",
                COALESCE(sum(booking.management_fee_minor), 0)::bigint AS "ManagementFeeMinor",
                COALESCE(sum(booking.fees_minor), 0)::bigint AS "FeesMinor",
                COALESCE(sum(booking.vat_minor), 0)::bigint AS "VatMinor",
                COALESCE(sum(booking.client_price_minor), 0)::bigint AS "ClientTotalMinor",
                COALESCE(min(booking.currency_code), {MasterDataCodes.Currencies.Zar}) AS "Currency"
            FROM commercial.bookings booking
            JOIN commercial.proposal_versions proposal
              ON proposal.tenant_id = booking.buyer_tenant_id
              AND proposal.id = booking.proposal_version_id
            JOIN commercial.campaign_briefs brief ON brief.tenant_id = proposal.tenant_id
              AND brief.id = proposal.brief_id
            WHERE booking.buyer_tenant_id = {tenantId.Value}
              AND ({query.From}::date IS NULL OR booking.created_at_utc >= {query.From}::date)
              AND ({query.To}::date IS NULL OR booking.created_at_utc < ({query.To}::date + 1))
              AND ({query.ClientAccountId}::uuid IS NULL OR brief.client_account_id = {query.ClientAccountId})
              AND ({query.Channel}::text IS NULL OR booking.channel_code = {query.Channel})
              AND ({query.SupplierId}::uuid IS NULL OR booking.supplier_id = {query.SupplierId})
              AND ({query.Status}::text IS NULL OR booking.status_code = {query.Status})
              AND ({query.ReviewerUserId}::uuid IS NULL OR proposal.approved_by = {query.ReviewerUserId})
            """).SingleAsync(cancellationToken);

    private Task<List<ReportingStatusRow>> ReadStatusesAsync(
        TenantId tenantId,
        ReportingQuery query,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<ReportingStatusRow>($"""
            SELECT area AS "Area", status AS "Status", count(*)::int AS "Count",
                GREATEST(0, floor(extract(epoch FROM (clock_timestamp() - min(updated_at))) / 86400))::int
                    AS "OldestAgeDays"
            FROM (
                SELECT 'Opportunity'::text area, stage_code::text status, updated_at_utc updated_at,
                    client_account_id, owner_user_id
                FROM commercial.opportunities WHERE tenant_id = {tenantId.Value}
                UNION ALL
                SELECT 'Brief', status_code, updated_at_utc, client_account_id, owner_user_id
                FROM commercial.campaign_briefs WHERE tenant_id = {tenantId.Value}
                UNION ALL
                SELECT 'Proposal', proposal.status_code, proposal.created_at_utc,
                    brief.client_account_id, proposal.created_by
                FROM commercial.proposal_versions proposal
                JOIN commercial.campaign_briefs brief ON brief.tenant_id = proposal.tenant_id
                  AND brief.id = proposal.brief_id WHERE proposal.tenant_id = {tenantId.Value}
                UNION ALL
                SELECT 'Campaign', campaign.status_code, campaign.updated_at_utc,
                    brief.client_account_id, campaign.owner_user_id
                FROM commercial.campaigns campaign
                JOIN commercial.campaign_briefs brief ON brief.tenant_id = campaign.tenant_id
                  AND brief.id = campaign.brief_id WHERE campaign.tenant_id = {tenantId.Value}
            ) item
            WHERE ({query.From}::date IS NULL OR item.updated_at >= {query.From}::date)
              AND ({query.To}::date IS NULL OR item.updated_at < ({query.To}::date + 1))
              AND ({query.ClientAccountId}::uuid IS NULL OR item.client_account_id = {query.ClientAccountId})
              AND ({query.OwnerUserId}::uuid IS NULL OR item.owner_user_id = {query.OwnerUserId})
              AND ({query.Status}::text IS NULL OR item.status = {query.Status})
            GROUP BY area, status ORDER BY area, status
            """).ToListAsync(cancellationToken);

    private Task<List<ReportingExceptionRow>> ReadExceptionsAsync(
        TenantId tenantId,
        ReportingQuery query,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<ReportingExceptionRow>($"""
            SELECT code AS "Code", label AS "Label", count_value::int AS "Count"
            FROM (VALUES
              ('AUDIENCE_APPROVAL_PENDING', 'Audience strategies awaiting approval',
                (SELECT count(*) FROM commercial.audience_definition_sets
                 WHERE tenant_id = {tenantId.Value}
                   AND status_code <> {MasterDataCodes.LifecycleStatuses.Approved})),
              ('PLAN_SUPPLY_UNCONFIRMED', 'Plans with supply confidence below confirmed',
                (SELECT count(*) FROM commercial.media_plan_versions
                 WHERE tenant_id = {tenantId.Value}
                   AND supply_confidence_code <> {MasterDataCodes.SupplyConfidenceStatuses.Confirmed})),
              ('PROPOSAL_INVENTORY_REVIEW', 'Proposals blocked by changed inventory',
                (SELECT count(*) FROM commercial.proposal_versions
                 WHERE tenant_id = {tenantId.Value}
                   AND inventory_review_status_code <>
                       {MasterDataCodes.ProposalInventoryReviewStatuses.Current})),
              ('PROPOSAL_BRANDING_OUTSTANDING', 'Proposals with branding outstanding',
                (SELECT count(*) FROM commercial.proposal_versions proposal
                 LEFT JOIN commercial.workspace_brand_assets agency
                   ON agency.tenant_id = proposal.tenant_id AND agency.id = proposal.agency_brand_asset_id
                 LEFT JOIN commercial.workspace_brand_assets client
                   ON client.tenant_id = proposal.tenant_id AND client.id = proposal.client_brand_asset_id
                 WHERE proposal.tenant_id = {tenantId.Value}
                   AND proposal.unbranded_approved_at_utc IS NULL
                   AND (agency.approved_at_utc IS NULL OR client.approved_at_utc IS NULL))),
              ('BOOKING_ACTION_REQUIRED', 'Bookings not yet confirmed',
                (SELECT count(*) FROM commercial.bookings
                 WHERE buyer_tenant_id = {tenantId.Value}
                   AND status_code <> {MasterDataCodes.LifecycleStatuses.Confirmed})),
              ('DELIVERY_EVIDENCE_REVIEW', 'Delivery evidence awaiting review',
                (SELECT count(*) FROM commercial.delivery_proofs
                 WHERE buyer_tenant_id = {tenantId.Value}
                   AND status_code = {MasterDataCodes.LifecycleStatuses.Submitted}))
            ) item(code, label, count_value)
            WHERE count_value > 0
            ORDER BY code
            """).ToListAsync(cancellationToken);

    private Task<List<ReportingDimensionRow>> ReadDimensionsAsync(
        TenantId tenantId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<ReportingDimensionRow>($"""
            SELECT 'client-dimension'::text AS "Kind", id AS "Id", trading_name AS "Label"
            FROM commercial.client_accounts WHERE tenant_id = {tenantId.Value}
            UNION ALL
            SELECT 'campaign-dimension', id, title FROM commercial.campaigns
            WHERE tenant_id = {tenantId.Value}
            UNION ALL
            SELECT DISTINCT 'supplier-dimension', version.supplier_id, version.supplier_name
            FROM commercial.marketplace_listing_versions version
            UNION ALL
            SELECT 'user-dimension', users.id, users.display_name
            FROM commercial.memberships membership
            JOIN commercial.users users ON users.id = membership.user_id
            WHERE membership.tenant_id = {tenantId.Value}
              AND membership.status_code = {MasterDataCodes.LifecycleStatuses.Active}
            ORDER BY "Kind", "Label", "Id"
            """).ToListAsync(cancellationToken);
}

internal sealed record OperationalReportingData(
    ReportingMetricRow Metrics,
    IReadOnlyList<ReportingChannelSpendRow> Channels,
    ReportingCommercialRow Commercial,
    IReadOnlyList<ReportingStatusRow> Statuses,
    IReadOnlyList<ReportingExceptionRow> Exceptions,
    IReadOnlyList<ReportingDimensionRow> Dimensions);

internal sealed record ReportingMetricRow(
    int Opportunities,
    int Briefs,
    int ApprovedAudiences,
    int PendingApprovals,
    int ApprovedPlans,
    int InventoryRecommendations,
    int SelectedInventory,
    int PublishedListings,
    int Proposals,
    int Bookings,
    int Campaigns,
    int DeliveryEvidenceItems,
    int MeasurementReports);

internal sealed record ReportingChannelSpendRow(
    string Channel,
    int PlanCount,
    long SupplierCostMinor,
    long ClientPriceMinor,
    long FeesMinor,
    long VatMinor,
    string Currency);

internal sealed record ReportingCommercialRow(
    int BookingCount,
    long SupplierCostMinor,
    long MarkupMinor,
    long CommissionMinor,
    long ManagementFeeMinor,
    long FeesMinor,
    long VatMinor,
    long ClientTotalMinor,
    string Currency);

internal sealed record ReportingStatusRow(
    string Area,
    string Status,
    int Count,
    int OldestAgeDays);

internal sealed record ReportingExceptionRow(
    string Code,
    string Label,
    int Count);

internal sealed record ReportingDimensionRow(string Kind, Guid Id, string Label);
