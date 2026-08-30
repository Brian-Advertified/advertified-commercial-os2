using System.Runtime.CompilerServices;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Advertified.Commercial.Infrastructure.Marketplace;

public sealed class MarketplaceRecordStore(GovernanceDbContext dbContext)
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

    internal Task<MarketplaceListingRow?> FindListingAsync(
        Guid listingId,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        var locking = forUpdate ? " FOR UPDATE OF listing" : string.Empty;
        return dbContext.Database.SqlQuery<MarketplaceListingRow>(
                FormattableStringFactory.Create(
                    ListingSelect + " WHERE listing.id = {0}" + locking, listingId))
            .SingleOrDefaultAsync(cancellationToken);
    }

    internal Task<MarketplaceRfqRow?> FindRfqAsync(
        Guid rfqId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<MarketplaceRfqRow>(
            FormattableStringFactory.Create(
                RfqSelect + " WHERE rfq.id = {6}", RfqParameters(now, rfqId)))
            .SingleOrDefaultAsync(cancellationToken);

    internal Task<MarketplaceRfqRow?> FindRfqByResponseAsync(
        Guid responseId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<MarketplaceRfqRow>(
            FormattableStringFactory.Create(
                RfqSelect + " WHERE response.id = {6}", RfqParameters(now, responseId)))
            .SingleOrDefaultAsync(cancellationToken);

    internal Task<MarketplaceProductSnapshotRow?> FindProductSnapshotAsync(
        TenantId tenantId,
        Guid productId,
        DateOnly today,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<MarketplaceProductSnapshotRow>($"""
            SELECT product.id AS "ProductId", version.id AS "ProductVersionId",
                product.supplier_id AS "SupplierId", rate.id AS "RateId",
                availability.id AS "AvailabilityId",
                supplier.name AS "SupplierName", version.name AS "ProductName",
                version.channel_code AS "Channel", version.product_type_code AS "ProductType",
                version.geography AS "Geography", rate.rate_type_code AS "RateType",
                rate.amount_minor AS "AmountMinor", rate.currency_code AS "Currency",
                rate.effective_from AS "RateEffectiveFrom",
                rate.effective_to AS "RateEffectiveTo",
                availability.availability_code AS "Availability",
                availability.observed_at_utc AS "AvailabilityObservedAtUtc",
                availability.valid_until_utc AS "AvailabilityValidUntilUtc"
            FROM commercial.inventory_products product
            JOIN commercial.inventory_suppliers supplier
              ON supplier.tenant_id = product.tenant_id AND supplier.id = product.supplier_id
            JOIN commercial.inventory_product_versions version
              ON version.tenant_id = product.tenant_id AND version.id = product.current_version_id
            JOIN LATERAL (
                SELECT item.*
                FROM commercial.inventory_rates item
                WHERE item.tenant_id = version.tenant_id
                  AND item.product_version_id = version.id
                ORDER BY ((item.effective_from IS NULL OR item.effective_from <= {today})
                    AND (item.effective_to IS NULL OR item.effective_to >= {today})) DESC,
                    item.effective_from DESC NULLS LAST, item.id DESC
                LIMIT 1) rate ON TRUE
            JOIN LATERAL (
                SELECT item.*
                FROM commercial.inventory_availability item
                WHERE item.tenant_id = version.tenant_id
                  AND item.product_version_id = version.id
                ORDER BY (item.observed_at_utc IS NULL OR
                    item.observed_at_utc <= {now}) DESC,
                    item.observed_at_utc DESC NULLS LAST, item.id DESC
                LIMIT 1) availability ON TRUE
            WHERE product.tenant_id = {tenantId.Value} AND product.id = {productId}
              AND product.status_code = {MasterDataCodes.LifecycleStatuses.Active}
            """).SingleOrDefaultAsync(cancellationToken);

    internal const string ListingSelect = """
        SELECT listing.id AS "Id", listing.supplier_tenant_id AS "SupplierTenantId",
            listing.product_id AS "ProductId", listing.status_code AS "Status",
            listing.terms AS "ListingTerms",
            listing.version AS "Version", listing.updated_at_utc AS "UpdatedAtUtc",
            version.id AS "ListingVersionId", version.version_number AS "ListingVersionNumber",
            version.product_version_id AS "ProductVersionId", version.rate_id AS "RateId",
            version.availability_id AS "AvailabilityId", version.supplier_name AS "SupplierName",
            version.product_name AS "ProductName", version.channel_code AS "Channel",
            version.product_type_code AS "ProductType", version.geography AS "Geography",
            version.rate_type_code AS "RateType", version.amount_minor AS "AmountMinor",
            version.currency_code AS "Currency", version.availability_code AS "Availability",
            version.availability_valid_until_utc AS "AvailabilityValidUntilUtc",
            version.terms AS "Terms", version.published_by AS "PublishedBy",
            version.published_at_utc AS "PublishedAtUtc"
        FROM commercial.marketplace_listings listing
        LEFT JOIN commercial.marketplace_listing_versions version
          ON version.supplier_tenant_id = listing.supplier_tenant_id
         AND version.id = listing.current_version_id
        """;

    internal const string RfqSelect = """
        SELECT rfq.id AS "Id", rfq.buyer_tenant_id AS "BuyerTenantId",
            rfq.supplier_tenant_id AS "SupplierTenantId",
            rfq.listing_version_id AS "ListingVersionId",
            listing.supplier_name AS "SupplierName", listing.product_name AS "ProductName",
            rfq.subject AS "Subject", rfq.requested_start AS "RequestedStart",
            rfq.requested_end AS "RequestedEnd", rfq.quantity AS "Quantity",
            rfq.due_at_utc AS "DueAtUtc",
            CASE WHEN acceptance.id IS NOT NULL THEN {1}
                 WHEN response.id IS NOT NULL AND response.valid_until_utc <= {0} THEN {2}
                 WHEN response.id IS NOT NULL THEN {3}
                 WHEN rfq.sent_at_utc IS NULL THEN {4}
                 WHEN rfq.due_at_utc <= {0} THEN {2} ELSE {5} END AS "Status",
            rfq.created_by AS "CreatedBy", rfq.sent_by AS "SentBy",
            rfq.sent_at_utc AS "SentAtUtc", rfq.version AS "Version",
            rfq.updated_at_utc AS "UpdatedAtUtc", response.id AS "ResponseId",
            (response.response_version + CASE WHEN acceptance.id IS NULL THEN 0 ELSE 1 END)
                AS "ResponseVersion",
            response.amount_minor AS "ResponseAmountMinor",
            response.currency_code AS "ResponseCurrency",
            response.availability_code AS "ResponseAvailability",
            response.terms AS "ResponseTerms",
            response.valid_until_utc AS "ResponseValidUntilUtc",
            response.evidence_references_json::text AS "EvidenceJson",
            response.submitted_by AS "SubmittedBy",
            response.submitted_at_utc AS "SubmittedAtUtc",
            acceptance.accepted_by AS "AcceptedBy",
            acceptance.accepted_at_utc AS "AcceptedAtUtc"
        FROM commercial.marketplace_rfqs rfq
        JOIN commercial.marketplace_listing_versions listing
          ON listing.supplier_tenant_id = rfq.supplier_tenant_id
         AND listing.id = rfq.listing_version_id
        LEFT JOIN LATERAL (
            SELECT value.* FROM commercial.marketplace_supplier_responses value
            WHERE value.rfq_id = rfq.id
            ORDER BY value.response_version DESC LIMIT 1) response ON true
        LEFT JOIN commercial.marketplace_response_acceptances acceptance
          ON acceptance.response_id = response.id
        """;

    internal static object?[] RfqParameters(
        DateTimeOffset now, params object?[] suffix) =>
        [now, MasterDataCodes.MarketplaceRfqStatuses.Accepted,
            MasterDataCodes.MarketplaceRfqStatuses.Expired,
            MasterDataCodes.MarketplaceRfqStatuses.Responded,
            MasterDataCodes.MarketplaceRfqStatuses.Draft,
            MasterDataCodes.MarketplaceRfqStatuses.Sent, .. suffix];
}
