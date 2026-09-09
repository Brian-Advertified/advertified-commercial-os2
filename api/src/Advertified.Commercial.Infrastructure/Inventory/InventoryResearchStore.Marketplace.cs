using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal sealed partial class InventoryResearchStore
{
    private async Task RefreshPublishedMarketplaceAudienceAsync(
        TenantId tenantId,
        Guid productId,
        Guid previousProductVersionId,
        Guid newProductVersionId,
        string audienceProfileJson,
        Guid actorId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var listing = await LockPublishedListingAsync(
            tenantId, productId, cancellationToken);
        if (listing is null || listing.ProductVersionId != previousProductVersionId) return;

        var newListingVersionId = Guid.NewGuid();
        await ClonePublishedListingVersionAsync(
            tenantId, listing, newListingVersionId, newProductVersionId,
            audienceProfileJson, actorId, now, cancellationToken);
        await AdvancePublishedListingAsync(
            tenantId, listing, newListingVersionId, now, cancellationToken);
    }

    private Task<InventoryResearchMarketplaceRow?> LockPublishedListingAsync(
        TenantId tenantId, Guid productId, CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<InventoryResearchMarketplaceRow>($"""
            SELECT listing.id AS "ListingId", snapshot.id AS "ListingVersionId",
                snapshot.product_version_id AS "ProductVersionId",
                snapshot.version_number AS "ListingVersionNumber", listing.version AS "ListingVersion"
            FROM commercial.marketplace_listings listing
            JOIN commercial.marketplace_listing_versions snapshot
              ON snapshot.supplier_tenant_id = listing.supplier_tenant_id
             AND snapshot.id = listing.current_version_id
            WHERE listing.supplier_tenant_id = {tenantId.Value}
              AND listing.product_id = {productId}
              AND listing.status_code = {MasterDataCodes.MarketplaceListingStatuses.Published}
            FOR UPDATE OF listing
            """).SingleOrDefaultAsync(cancellationToken);

    private async Task ClonePublishedListingVersionAsync(
        TenantId tenantId,
        InventoryResearchMarketplaceRow listing,
        Guid newListingVersionId,
        Guid newProductVersionId,
        string audienceProfileJson,
        Guid actorId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var inserted = await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.marketplace_listing_versions (
                id, supplier_tenant_id, listing_id, version_number,
                product_version_id, supplier_id, rate_id, availability_id, supplier_name,
                product_name, channel_code, product_type_code, geography,
                outlet_id, outlet_name, outlet_identity_basis, outlet_source_locator,
                audience_profile_json, rate_type_code, amount_minor, currency_code,
                availability_code, rate_effective_from, rate_effective_to, rate_source_locator,
                availability_source_locator, availability_observed_at_utc,
                availability_valid_until_utc, supplier_vat_status_code, vat_treatment_code,
                supplier_commercial_json, commercial_terms_json, deliverable_json, spatial_json,
                logo_asset_id, private_spatial_location, private_coverage_geometry,
                private_catchment_geometry, private_route_geometry,
                terms, published_by, published_at_utc)
            SELECT {newListingVersionId}, snapshot.supplier_tenant_id, snapshot.listing_id,
                {listing.ListingVersionNumber + 1}, {newProductVersionId}, snapshot.supplier_id,
                snapshot.rate_id, snapshot.availability_id, snapshot.supplier_name,
                snapshot.product_name, snapshot.channel_code, snapshot.product_type_code,
                snapshot.geography, snapshot.outlet_id, snapshot.outlet_name,
                snapshot.outlet_identity_basis, snapshot.outlet_source_locator,
                {audienceProfileJson}::jsonb, snapshot.rate_type_code, snapshot.amount_minor,
                snapshot.currency_code, snapshot.availability_code, snapshot.rate_effective_from,
                snapshot.rate_effective_to, snapshot.rate_source_locator,
                snapshot.availability_source_locator, snapshot.availability_observed_at_utc,
                snapshot.availability_valid_until_utc, snapshot.supplier_vat_status_code,
                snapshot.vat_treatment_code, snapshot.supplier_commercial_json,
                snapshot.commercial_terms_json, snapshot.deliverable_json, snapshot.spatial_json,
                snapshot.logo_asset_id, snapshot.private_spatial_location,
                snapshot.private_coverage_geometry, snapshot.private_catchment_geometry,
                snapshot.private_route_geometry, snapshot.terms, {actorId}, {now}
            FROM commercial.marketplace_listing_versions snapshot
            WHERE snapshot.supplier_tenant_id = {tenantId.Value}
              AND snapshot.id = {listing.ListingVersionId}
            """, cancellationToken);
        if (inserted != 1) throw new VersionConflictException();
    }

    private async Task AdvancePublishedListingAsync(
        TenantId tenantId,
        InventoryResearchMarketplaceRow listing,
        Guid newListingVersionId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.marketplace_listings
            SET current_version_id = {newListingVersionId}, version = version + 1,
                updated_at_utc = {now}
            WHERE supplier_tenant_id = {tenantId.Value} AND id = {listing.ListingId}
              AND current_version_id = {listing.ListingVersionId}
              AND version = {listing.ListingVersion}
              AND status_code = {MasterDataCodes.MarketplaceListingStatuses.Published}
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
    }
}
