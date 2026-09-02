using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Advertified.Commercial.Infrastructure.Booking;

public sealed partial class BookingRecordStore(GovernanceDbContext dbContext)
{
    private const int MaxBookingResults = 200;

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

    internal const string BookingSelect = """
        SELECT booking.id AS "Id", booking.buyer_tenant_id AS "BuyerTenantId",
            booking.supplier_tenant_id AS "SupplierTenantId",
            CASE WHEN booking.buyer_tenant_id = commercial.current_tenant_id()
                THEN booking.proposal_version_id END AS "ProposalVersionId",
            CASE WHEN booking.buyer_tenant_id = commercial.current_tenant_id()
                THEN booking.proposal_option_id END AS "ProposalOptionId",
            CASE WHEN booking.buyer_tenant_id = commercial.current_tenant_id()
                THEN booking.proposal_decision_id END AS "ProposalDecisionId",
            CASE WHEN booking.buyer_tenant_id = commercial.current_tenant_id()
                THEN booking.plan_version_id END AS "PlanVersionId",
            CASE WHEN booking.buyer_tenant_id = commercial.current_tenant_id()
                THEN booking.media_plan_line_id END AS "MediaPlanLineId",
            booking.marketplace_listing_version_id AS "MarketplaceListingVersionId",
            booking.inventory_product_id AS "InventoryProductId",
            booking.product_version_id AS "ProductVersionId",
            booking.commercial_policy_version_id AS "CommercialPolicyVersionId",
            booking.supplier_commercial_json::text AS "SupplierCommercialJson",
            booking.vat_treatment_code AS "VatTreatment",
            booking.commercial_terms_json::text AS "CommercialTermsJson",
            booking.deliverable_json::text AS "DeliverableJson",
            booking.spatial_json::text AS "SpatialJson",
            booking.logo_asset_id AS "LogoAssetId",
            booking.supplier_name AS "SupplierName",
            booking.product_name AS "ProductName", booking.channel_code AS "Channel",
            booking.geography AS "Geography", booking.flight_start AS "FlightStart",
            booking.flight_end AS "FlightEnd", booking.running_periods AS "RunningPeriods",
            booking.quantity AS "Quantity",
            CASE WHEN booking.supplier_tenant_id = commercial.current_tenant_id()
                      AND viewer.role_code IN ({0}, {1})
                THEN booking.supplier_cost_minor END AS "SupplierCostMinor",
            CASE WHEN booking.buyer_tenant_id = commercial.current_tenant_id()
                THEN booking.client_price_minor END AS "ClientPriceMinor",
            CASE WHEN booking.buyer_tenant_id = commercial.current_tenant_id()
                THEN booking.fees_minor END AS "FeesMinor",
            CASE WHEN booking.buyer_tenant_id = commercial.current_tenant_id()
                THEN booking.vat_minor END AS "VatMinor",
            booking.currency_code AS "Currency", booking.terms AS "Terms",
            booking.status_code AS "Status", booking.created_by AS "CreatedBy",
            booking.created_at_utc AS "CreatedAtUtc", booking.requested_by AS "RequestedBy",
            booking.requested_at_utc AS "RequestedAtUtc",
            booking.request_reason AS "RequestReason", booking.confirmed_by AS "ConfirmedBy",
            booking.confirmed_at_utc AS "ConfirmedAtUtc",
            booking.confirmation_reason AS "ConfirmationReason",
            CASE WHEN booking.supplier_tenant_id = commercial.current_tenant_id()
                      AND viewer.role_code IN ({0}, {1})
                THEN booking.supplier_note END AS "SupplierNote",
            booking.terms_accepted AS "TermsAccepted", booking.version AS "Version",
            booking.updated_at_utc AS "UpdatedAtUtc"
        FROM commercial.bookings booking
        JOIN commercial.memberships viewer
          ON viewer.tenant_id = commercial.current_tenant_id()
         AND viewer.user_id = commercial.current_user_id()
         AND viewer.status_code = {2}
        WHERE (booking.buyer_tenant_id = commercial.current_tenant_id()
               OR booking.supplier_tenant_id = commercial.current_tenant_id())
        """;
}
