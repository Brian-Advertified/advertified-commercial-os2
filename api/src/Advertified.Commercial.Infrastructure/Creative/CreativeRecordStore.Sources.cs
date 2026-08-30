using System.Runtime.CompilerServices;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Creative;

public sealed partial class CreativeRecordStore
{
    internal Task<List<CreativeBookingSourceRow>> ListConfirmedBookingsAsync(
        Guid campaignId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<CreativeBookingSourceRow>($"""
            SELECT booking.id AS "BookingId",
                booking.supplier_tenant_id AS "SupplierTenantId",
                booking.media_plan_line_id AS "MediaPlanLineId",
                booking.channel_code AS "Channel",
                booking.flight_start AS "FlightStart", booking.flight_end AS "FlightEnd",
                booking.version AS "BookingVersion", booking.currency_code AS "Currency",
                booking.supplier_cost_minor AS "SupplierCostMinor",
                booking.client_price_minor AS "ClientPriceMinor",
                booking.fees_minor AS "FeesMinor", booking.vat_minor AS "VatMinor"
            FROM commercial.campaigns campaign
            JOIN commercial.bookings booking
              ON booking.buyer_tenant_id = campaign.tenant_id
             AND booking.proposal_decision_id = campaign.proposal_decision_id
             AND booking.plan_version_id = campaign.plan_version_id
            WHERE campaign.id = {campaignId}
              AND campaign.status_code = {MasterDataCodes.LifecycleStatuses.Booked}
              AND booking.status_code = {MasterDataCodes.LifecycleStatuses.Confirmed}
            ORDER BY booking.id
            """).ToListAsync(cancellationToken);

    internal Task<CreativeRequirementSourceRow?> FindRequirementAsync(
        Guid campaignId,
        Guid requirementId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<CreativeRequirementSourceRow>($"""
            SELECT requirement.id AS "Id", requirement.campaign_id AS "CampaignId",
                requirement.booking_id AS "BookingId",
                requirement.media_plan_line_id AS "MediaPlanLineId",
                requirement.supplier_tenant_id AS "SupplierTenantId",
                requirement.channel_code AS "Channel",
                requirement.flight_start AS "FlightStart",
                requirement.flight_end AS "FlightEnd",
                requirement.format_code AS "FormatCode", requirement.width AS "Width",
                requirement.height AS "Height",
                requirement.required_media_type AS "RequiredMediaType",
                requirement.maximum_bytes AS "MaximumBytes",
                requirement.instructions AS "Instructions",
                campaign.version AS "CampaignVersion", booking.version AS "BookingVersion",
                booking.currency_code AS "Currency",
                booking.supplier_cost_minor AS "SupplierCostMinor",
                booking.client_price_minor AS "ClientPriceMinor",
                booking.fees_minor AS "FeesMinor", booking.vat_minor AS "VatMinor"
            FROM commercial.creative_requirements requirement
            JOIN commercial.campaigns campaign
              ON campaign.tenant_id = requirement.buyer_tenant_id
             AND campaign.id = requirement.campaign_id
            JOIN commercial.bookings booking
              ON booking.buyer_tenant_id = requirement.buyer_tenant_id
             AND booking.supplier_tenant_id = requirement.supplier_tenant_id
             AND booking.id = requirement.booking_id
            WHERE requirement.campaign_id = {campaignId} AND requirement.id = {requirementId}
              AND campaign.status_code = {MasterDataCodes.LifecycleStatuses.CreativePending}
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<CreativeAssetStateRow?> FindAssetAsync(
        Guid assetId,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        var sql = """
            SELECT asset.id AS "Id", asset.buyer_tenant_id AS "BuyerTenantId",
                asset.campaign_id AS "CampaignId",
                asset.requirement_id AS "RequirementId",
                asset.supplier_tenant_id AS "SupplierTenantId",
                asset.current_version_id AS "CurrentVersionId",
                version.version_number AS "CurrentVersionNumber", asset.version AS "Version"
            FROM commercial.creative_assets asset
            LEFT JOIN commercial.creative_asset_versions version
              ON version.buyer_tenant_id = asset.buyer_tenant_id
             AND version.id = asset.current_version_id
            WHERE asset.id = {0}
            """ + (forUpdate ? " FOR UPDATE OF asset" : string.Empty);
        return DbContext.Database.SqlQuery<CreativeAssetStateRow>(
            FormattableStringFactory.Create(sql, assetId))
            .SingleOrDefaultAsync(cancellationToken);
    }

    internal Task<CreativeAssetStateRow?> FindAssetForRequirementAsync(
        Guid requirementId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<CreativeAssetStateRow>($"""
            SELECT asset.id AS "Id", asset.buyer_tenant_id AS "BuyerTenantId",
                asset.campaign_id AS "CampaignId",
                asset.requirement_id AS "RequirementId",
                asset.supplier_tenant_id AS "SupplierTenantId",
                asset.current_version_id AS "CurrentVersionId",
                version.version_number AS "CurrentVersionNumber", asset.version AS "Version"
            FROM commercial.creative_assets asset
            LEFT JOIN commercial.creative_asset_versions version
              ON version.buyer_tenant_id = asset.buyer_tenant_id
             AND version.id = asset.current_version_id
            WHERE asset.requirement_id = {requirementId}
            FOR UPDATE OF asset
            """).SingleOrDefaultAsync(cancellationToken);
}
