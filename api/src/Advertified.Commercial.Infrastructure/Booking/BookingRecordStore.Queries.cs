using System.Runtime.CompilerServices;
using Advertified.Commercial.Application.Booking;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Booking;

public sealed partial class BookingRecordStore
{
    internal Task<BookingSourceRow?> FindSelectedSourceAsync(
        TenantId tenantId,
        CreateBookingCommand command,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<BookingSourceRow>($"""
            SELECT proposal.id AS "ProposalVersionId", option.id AS "ProposalOptionId",
                decision.id AS "ProposalDecisionId", plan.id AS "PlanVersionId",
                line.id AS "MediaPlanLineId",
                plan.commercial_policy_version_id AS "CommercialPolicyVersionId",
                line.inventory_tenant_id AS "SupplierTenantId",
                line.marketplace_listing_version_id AS "MarketplaceListingVersionId",
                snapshot.supplier_id AS "SupplierId",
                line.inventory_product_id AS "InventoryProductId",
                line.product_version_id AS "ProductVersionId", line.rate_id AS "RateId",
                line.availability_id AS "AvailabilityId",
                snapshot.supplier_name AS "SupplierName", line.product_name AS "ProductName",
                line.channel_code AS "Channel", line.geography AS "Geography",
                line.flight_start AS "FlightStart", line.flight_end AS "FlightEnd",
                jsonb_array_length(line.running_periods_json)::integer AS "RunningPeriods",
                line.quantity AS "Quantity", line.supplier_cost_minor AS "SupplierCostMinor",
                line.client_price_minor AS "ClientPriceMinor", line.fees_minor AS "FeesMinor",
                line.vat_minor AS "VatMinor", plan.currency_code AS "Currency",
                line.supplier_commercial_json::text AS "SupplierCommercialJson",
                line.vat_treatment_code AS "VatTreatment",
                line.commercial_terms_json::text AS "CommercialTermsJson",
                line.deliverable_json::text AS "DeliverableJson",
                line.spatial_json::text AS "SpatialJson",
                line.logo_asset_id AS "LogoAssetId"
            FROM commercial.proposal_versions proposal
            JOIN commercial.proposal_options option
              ON option.tenant_id = proposal.tenant_id
             AND option.proposal_version_id = proposal.id
            JOIN commercial.proposal_decisions decision
              ON decision.tenant_id = proposal.tenant_id
             AND decision.proposal_version_id = proposal.id
             AND decision.option_id = option.id
            JOIN commercial.media_plan_versions plan
              ON plan.tenant_id = option.tenant_id AND plan.id = option.plan_version_id
            JOIN commercial.media_plan_lines line
              ON line.tenant_id = plan.tenant_id AND line.plan_version_id = plan.id
            JOIN commercial.campaigns campaign
              ON campaign.tenant_id = decision.tenant_id
             AND campaign.proposal_decision_id = decision.id
             AND campaign.plan_version_id = plan.id
            JOIN commercial.payment_intents payment
              ON payment.tenant_id = campaign.tenant_id
             AND payment.id = campaign.payment_intent_id
            JOIN commercial.marketplace_listing_versions snapshot
              ON snapshot.supplier_tenant_id = line.inventory_tenant_id
             AND snapshot.id = line.marketplace_listing_version_id
             AND snapshot.product_version_id = line.product_version_id
             AND snapshot.rate_id = line.rate_id
             AND snapshot.availability_id = line.availability_id
            JOIN commercial.marketplace_listings listing
              ON listing.supplier_tenant_id = snapshot.supplier_tenant_id
             AND listing.id = snapshot.listing_id
             AND listing.current_version_id = snapshot.id
            WHERE proposal.tenant_id = {tenantId.Value}
              AND proposal.id = {command.ProposalVersionId}
              AND option.id = {command.ProposalOptionId}
              AND line.id = {command.MediaPlanLineId}
              AND proposal.status_code = {MasterDataCodes.LifecycleStatuses.Selected}
              AND proposal.inventory_review_status_code =
                    {MasterDataCodes.ProposalInventoryReviewStatuses.Current}
              AND decision.decision_code = {MasterDataCodes.LifecycleStatuses.Selected}
              AND plan.status_code = {MasterDataCodes.LifecycleStatuses.Approved}
              AND plan.commercial_policy_version_id IS NOT NULL
              AND campaign.status_code = {MasterDataCodes.LifecycleStatuses.Planned}
              AND payment.status_code = {MasterDataCodes.LifecycleStatuses.Confirmed}
              AND listing.status_code = {MasterDataCodes.MarketplaceListingStatuses.Published}
              AND snapshot.availability_code = {MasterDataCodes.AvailabilityStatuses.Available}
              AND (snapshot.availability_observed_at_utc IS NULL
                   OR snapshot.availability_observed_at_utc <= {now})
              AND (snapshot.availability_valid_until_utc IS NULL
                   OR snapshot.availability_valid_until_utc::date >= line.flight_end)
              AND (snapshot.rate_effective_from IS NULL
                   OR snapshot.rate_effective_from <= line.flight_start)
              AND (snapshot.rate_effective_to IS NULL
                   OR snapshot.rate_effective_to >= line.flight_end)
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<BookingRow?> FindAsync(
        Guid bookingId,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        var suffix = forUpdate ? " FOR UPDATE OF booking" : string.Empty;
        return DbContext.Database.SqlQuery<BookingRow>(FormattableStringFactory.Create(
                BookingSelect + " AND booking.id = {3}" + suffix,
                BookingProjectionArguments(bookingId)))
            .SingleOrDefaultAsync(cancellationToken);
    }

    internal Task<List<BookingRow>> ListRowsAsync(CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<BookingRow>(FormattableStringFactory.Create(
                BookingSelect +
                " ORDER BY booking.updated_at_utc DESC, booking.id DESC LIMIT {3}",
                BookingProjectionArguments(MaxBookingResults)))
            .ToListAsync(cancellationToken);

    private static object[] BookingProjectionArguments(params object[] suffix) =>
    [
        MasterDataCodes.Roles.SupplierAdmin,
        MasterDataCodes.Roles.SupplierUser,
        MasterDataCodes.LifecycleStatuses.Active,
        .. suffix,
    ];

    internal Task<List<BookableLineRow>> ListBookableRowsAsync(
        TenantId tenantId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<BookableLineRow>($"""
            SELECT proposal.id AS "ProposalVersionId", option.id AS "ProposalOptionId",
                decision.id AS "ProposalDecisionId", plan.id AS "PlanVersionId",
                line.id AS "MediaPlanLineId", snapshot.supplier_name AS "SupplierName",
                line.product_name AS "ProductName", line.channel_code AS "Channel",
                line.geography AS "Geography", line.flight_start AS "FlightStart",
                line.flight_end AS "FlightEnd",
                jsonb_array_length(line.running_periods_json)::integer AS "RunningPeriods",
                line.quantity AS "Quantity", line.client_price_minor AS "ClientPriceMinor",
                line.fees_minor AS "FeesMinor", line.vat_minor AS "VatMinor",
                plan.currency_code AS "Currency", (booking.id IS NOT NULL) AS "AlreadyBooked"
            FROM commercial.proposal_versions proposal
            JOIN commercial.proposal_decisions decision
              ON decision.tenant_id = proposal.tenant_id
             AND decision.proposal_version_id = proposal.id
            JOIN commercial.proposal_options option
              ON option.tenant_id = proposal.tenant_id AND option.id = decision.option_id
            JOIN commercial.media_plan_versions plan
              ON plan.tenant_id = option.tenant_id AND plan.id = option.plan_version_id
            JOIN commercial.media_plan_lines line
              ON line.tenant_id = plan.tenant_id AND line.plan_version_id = plan.id
            JOIN commercial.campaigns campaign
              ON campaign.tenant_id = decision.tenant_id
             AND campaign.proposal_decision_id = decision.id
             AND campaign.plan_version_id = plan.id
            JOIN commercial.payment_intents payment
              ON payment.tenant_id = campaign.tenant_id
             AND payment.id = campaign.payment_intent_id
            JOIN commercial.marketplace_listing_versions snapshot
              ON snapshot.supplier_tenant_id = line.inventory_tenant_id
             AND snapshot.id = line.marketplace_listing_version_id
            LEFT JOIN commercial.bookings booking
              ON booking.buyer_tenant_id = line.tenant_id
             AND booking.proposal_decision_id = decision.id
             AND booking.media_plan_line_id = line.id
            WHERE proposal.tenant_id = {tenantId.Value}
              AND proposal.status_code = {MasterDataCodes.LifecycleStatuses.Selected}
              AND proposal.inventory_review_status_code =
                    {MasterDataCodes.ProposalInventoryReviewStatuses.Current}
              AND decision.decision_code = {MasterDataCodes.LifecycleStatuses.Selected}
              AND campaign.status_code = {MasterDataCodes.LifecycleStatuses.Planned}
              AND payment.status_code = {MasterDataCodes.LifecycleStatuses.Confirmed}
            ORDER BY decision.decided_at_utc DESC, line.id
            """).ToListAsync(cancellationToken);

    internal Task<bool> IsSupplyCurrentAsync(
        BookingRow booking,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1
                FROM commercial.marketplace_listing_versions snapshot
                JOIN commercial.marketplace_listings listing
                  ON listing.supplier_tenant_id = snapshot.supplier_tenant_id
                 AND listing.id = snapshot.listing_id
                 AND listing.current_version_id = snapshot.id
                WHERE snapshot.supplier_tenant_id = {booking.SupplierTenantId}
                  AND snapshot.id = {booking.MarketplaceListingVersionId}
                  AND listing.status_code = {MasterDataCodes.MarketplaceListingStatuses.Published}
                  AND snapshot.availability_code = {MasterDataCodes.AvailabilityStatuses.Available}
                  AND (snapshot.availability_observed_at_utc IS NULL
                       OR snapshot.availability_observed_at_utc <= {now})
                  AND (snapshot.availability_valid_until_utc IS NULL
                       OR snapshot.availability_valid_until_utc::date >= {booking.FlightEnd})
                  AND (snapshot.rate_effective_from IS NULL
                       OR snapshot.rate_effective_from <= {booking.FlightStart})
                  AND (snapshot.rate_effective_to IS NULL
                       OR snapshot.rate_effective_to >= {booking.FlightEnd})) AS "Value"
            """).SingleAsync(cancellationToken);
}
