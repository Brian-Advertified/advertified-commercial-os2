using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Planning;

public sealed partial class PlanningRecordStore
{
    internal Task<List<PlanningInventoryRow>> ListInventoryAsync(
        TenantId tenantId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<PlanningInventoryRow>($"""
            SELECT product.tenant_id AS "InventoryTenantId",
                NULL::uuid AS "MarketplaceListingVersionId",
                product.id AS "ProductId", version.id AS "ProductVersionId",
                product.supplier_id AS "SupplierId", version.name AS "Name",
                version.channel_code AS "Channel", version.product_type_code AS "ProductType",
                version.geography AS "Geography", version.latitude AS "Latitude",
                version.longitude AS "Longitude", rate.id AS "RateId",
                rate.rate_type_code AS "RateType", rate.currency_code AS "Currency",
                rate.amount_minor AS "RateAmountMinor", rate.effective_from AS "EffectiveFrom",
                rate.effective_to AS "EffectiveTo", rate.source_locator AS "RateSource",
                availability.id AS "AvailabilityId",
                availability.availability_code AS "Availability",
                availability.observed_at_utc AS "ObservedAtUtc",
                availability.valid_until_utc AS "ValidUntilUtc",
                availability.source_locator AS "AvailabilitySource",
                COALESCE((SELECT jsonb_agg(jsonb_build_object(
                    'start', exception.starts_on, 'end', exception.ends_on,
                    'reason', exception.exception_type_code)
                    ORDER BY exception.starts_on, exception.ends_on, exception.id)
                    FROM commercial.inventory_availability_exceptions exception
                    WHERE exception.tenant_id = version.tenant_id
                      AND exception.product_id = product.id), '[]'::jsonb)::text
                    AS "UnavailablePeriodsJson",
                version.audience_profile_json::text AS "AudienceProfileJson",
                supplier_version.vat_status_code AS "SupplierVatStatus",
                CASE WHEN supplier_version.id IS NULL THEN NULL ELSE jsonb_build_object(
                    'vatStatus', supplier_version.vat_status_code,
                    'vatNumber', supplier_version.vat_number,
                    'commissionTerms', supplier_version.commission_terms,
                    'paymentTerms', supplier_version.payment_terms,
                    'cancellationTerms', supplier_version.cancellation_terms,
                    'bookingDeadlineTerms', supplier_version.booking_deadline_terms)
                    END::text AS "SupplierCommercialJson",
                rate.vat_treatment_code AS "VatTreatment",
                rate.commercial_terms_json::text AS "CommercialTermsJson",
                version.deliverable_json::text AS "DeliverableJson",
                version.spatial_json::text AS "SpatialJson",
                logo.id AS "LogoAssetId"
            FROM commercial.inventory_products product
            JOIN commercial.inventory_product_versions version
              ON version.tenant_id = product.tenant_id
             AND version.id = product.current_version_id
            JOIN commercial.inventory_suppliers supplier
              ON supplier.tenant_id = product.tenant_id
             AND supplier.id = product.supplier_id
            LEFT JOIN commercial.inventory_supplier_versions supplier_version
              ON supplier_version.tenant_id = supplier.tenant_id
             AND supplier_version.id = supplier.current_commercial_version_id
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
            LEFT JOIN LATERAL (
                SELECT asset.id
                FROM commercial.inventory_assets asset
                WHERE asset.tenant_id = version.tenant_id
                  AND asset.product_version_id = version.id
                  AND asset.asset_type_code = {MasterDataCodes.AssetTypes.Logo}
                  AND commercial.inventory_asset_rights_valid(
                      asset.id, {MasterDataCodes.AssetRightsScopes.NamedClientProposal},
                      'ZA', CURRENT_DATE)
                ORDER BY asset.id
                LIMIT 1) logo ON TRUE
            WHERE product.tenant_id = {tenantId.Value}
              AND product.status_code = {MasterDataCodes.LifecycleStatuses.Active}
              AND NOT EXISTS (
                  SELECT 1 FROM commercial.inventory_product_identity_links identity_link
                  WHERE identity_link.tenant_id = product.tenant_id
                    AND identity_link.duplicate_product_id = product.id)

            UNION ALL

            SELECT listing.supplier_tenant_id AS "InventoryTenantId",
                snapshot.id AS "MarketplaceListingVersionId",
                listing.product_id AS "ProductId",
                snapshot.product_version_id AS "ProductVersionId",
                snapshot.supplier_id AS "SupplierId", snapshot.product_name AS "Name",
                snapshot.channel_code AS "Channel",
                snapshot.product_type_code AS "ProductType",
                snapshot.geography AS "Geography", NULL::numeric AS "Latitude",
                NULL::numeric AS "Longitude", snapshot.rate_id AS "RateId",
                snapshot.rate_type_code AS "RateType", snapshot.currency_code AS "Currency",
                snapshot.amount_minor AS "RateAmountMinor",
                snapshot.rate_effective_from AS "EffectiveFrom",
                snapshot.rate_effective_to AS "EffectiveTo",
                snapshot.rate_source_locator AS "RateSource",
                snapshot.availability_id AS "AvailabilityId",
                snapshot.availability_code AS "Availability",
                snapshot.availability_observed_at_utc AS "ObservedAtUtc",
                snapshot.availability_valid_until_utc AS "ValidUntilUtc",
                snapshot.availability_source_locator AS "AvailabilitySource",
                commercial.marketplace_inventory_unavailable_periods(
                    listing.supplier_tenant_id, listing.product_id)::text
                    AS "UnavailablePeriodsJson",
                snapshot.audience_profile_json::text AS "AudienceProfileJson",
                snapshot.supplier_vat_status_code AS "SupplierVatStatus",
                snapshot.supplier_commercial_json::text AS "SupplierCommercialJson",
                snapshot.vat_treatment_code AS "VatTreatment",
                snapshot.commercial_terms_json::text AS "CommercialTermsJson",
                snapshot.deliverable_json::text AS "DeliverableJson",
                snapshot.spatial_json::text AS "SpatialJson",
                CASE WHEN commercial.inventory_asset_rights_valid(
                    snapshot.logo_asset_id,
                    {MasterDataCodes.AssetRightsScopes.NamedClientProposal},
                    'ZA', CURRENT_DATE)
                    THEN snapshot.logo_asset_id ELSE NULL END AS "LogoAssetId"
            FROM commercial.marketplace_listings listing
            JOIN commercial.marketplace_listing_versions snapshot
              ON snapshot.supplier_tenant_id = listing.supplier_tenant_id
             AND snapshot.id = listing.current_version_id
            WHERE listing.supplier_tenant_id <> {tenantId.Value}
              AND listing.status_code = {MasterDataCodes.MarketplaceListingStatuses.Published}
            ORDER BY "InventoryTenantId", "ProductId"
            """).ToListAsync(cancellationToken);

    internal async Task<PlanningInventoryRow?> FindInventoryAsync(
        TenantId tenantId,
        Guid productId,
        CancellationToken cancellationToken)
    {
        var rows = await DbContext.Database.SqlQuery<PlanningInventoryRow>($"""
            SELECT product.tenant_id AS "InventoryTenantId",
                NULL::uuid AS "MarketplaceListingVersionId",
                product.id AS "ProductId", version.id AS "ProductVersionId",
                product.supplier_id AS "SupplierId", version.name AS "Name",
                version.channel_code AS "Channel", version.product_type_code AS "ProductType",
                version.geography AS "Geography", version.latitude AS "Latitude",
                version.longitude AS "Longitude", rate.id AS "RateId",
                rate.rate_type_code AS "RateType", rate.currency_code AS "Currency",
                rate.amount_minor AS "RateAmountMinor", rate.effective_from AS "EffectiveFrom",
                rate.effective_to AS "EffectiveTo", rate.source_locator AS "RateSource",
                availability.id AS "AvailabilityId",
                availability.availability_code AS "Availability",
                availability.observed_at_utc AS "ObservedAtUtc",
                availability.valid_until_utc AS "ValidUntilUtc",
                availability.source_locator AS "AvailabilitySource",
                COALESCE((SELECT jsonb_agg(jsonb_build_object(
                    'start', exception.starts_on, 'end', exception.ends_on,
                    'reason', exception.exception_type_code)
                    ORDER BY exception.starts_on, exception.ends_on, exception.id)
                    FROM commercial.inventory_availability_exceptions exception
                    WHERE exception.tenant_id = version.tenant_id
                      AND exception.product_id = product.id), '[]'::jsonb)::text
                    AS "UnavailablePeriodsJson",
                version.audience_profile_json::text AS "AudienceProfileJson",
                supplier_version.vat_status_code AS "SupplierVatStatus",
                CASE WHEN supplier_version.id IS NULL THEN NULL ELSE jsonb_build_object(
                    'vatStatus', supplier_version.vat_status_code,
                    'vatNumber', supplier_version.vat_number,
                    'commissionTerms', supplier_version.commission_terms,
                    'paymentTerms', supplier_version.payment_terms,
                    'cancellationTerms', supplier_version.cancellation_terms,
                    'bookingDeadlineTerms', supplier_version.booking_deadline_terms)
                    END::text AS "SupplierCommercialJson",
                rate.vat_treatment_code AS "VatTreatment",
                rate.commercial_terms_json::text AS "CommercialTermsJson",
                version.deliverable_json::text AS "DeliverableJson",
                version.spatial_json::text AS "SpatialJson",
                logo.id AS "LogoAssetId"
            FROM commercial.inventory_products product
            JOIN commercial.inventory_product_versions version
              ON version.tenant_id = product.tenant_id
             AND version.id = product.current_version_id
            JOIN commercial.inventory_suppliers supplier
              ON supplier.tenant_id = product.tenant_id
             AND supplier.id = product.supplier_id
            LEFT JOIN commercial.inventory_supplier_versions supplier_version
              ON supplier_version.tenant_id = supplier.tenant_id
             AND supplier_version.id = supplier.current_commercial_version_id
            LEFT JOIN LATERAL (
                SELECT item.* FROM commercial.inventory_rates item
                WHERE item.tenant_id = version.tenant_id
                  AND item.product_version_id = version.id
                ORDER BY item.effective_from DESC NULLS LAST, item.id LIMIT 1) rate ON TRUE
            LEFT JOIN LATERAL (
                SELECT item.* FROM commercial.inventory_availability item
                WHERE item.tenant_id = version.tenant_id
                  AND item.product_version_id = version.id
                ORDER BY item.observed_at_utc DESC NULLS LAST, item.id LIMIT 1) availability ON TRUE
            LEFT JOIN LATERAL (
                SELECT asset.id
                FROM commercial.inventory_assets asset
                WHERE asset.tenant_id = version.tenant_id
                  AND asset.product_version_id = version.id
                  AND asset.asset_type_code = {MasterDataCodes.AssetTypes.Logo}
                  AND commercial.inventory_asset_rights_valid(
                      asset.id, {MasterDataCodes.AssetRightsScopes.NamedClientProposal},
                      'ZA', CURRENT_DATE)
                ORDER BY asset.id
                LIMIT 1) logo ON TRUE
            WHERE product.tenant_id = {tenantId.Value} AND product.id = {productId}
              AND product.status_code = {MasterDataCodes.LifecycleStatuses.Active}
              AND NOT EXISTS (
                  SELECT 1 FROM commercial.inventory_product_identity_links identity_link
                  WHERE identity_link.tenant_id = product.tenant_id
                    AND identity_link.duplicate_product_id = product.id)
            """).ToListAsync(cancellationToken);
        return rows.SingleOrDefault();
    }

    internal Task<List<PlanningSpatialPeerRow>> ListSpatialPeersAsync(
        TenantId tenantId,
        Guid targetProductVersionId,
        decimal maximumRadiusKilometres,
        CancellationToken cancellationToken) => ListSpatialPeersAsync(
            tenantId, [targetProductVersionId], maximumRadiusKilometres, cancellationToken);

    internal Task<List<PlanningSpatialPeerRow>> ListSpatialPeersAsync(
        TenantId tenantId,
        Guid[] targetProductVersionIds,
        decimal maximumRadiusKilometres,
        CancellationToken cancellationToken)
    {
        if (targetProductVersionIds.Length == 0)
        {
            return Task.FromResult(new List<PlanningSpatialPeerRow>());
        }
        var maximumRadiusMetres = maximumRadiusKilometres * 1000m;
        return DbContext.Database.SqlQuery<PlanningSpatialPeerRow>($"""
            SELECT target.id AS "TargetProductVersionId", peer.id AS "ProductVersionId",
                (ST_Distance(peer.spatial_location, target.spatial_location) / 1000.0)::numeric
                    AS "DistanceKilometres"
            FROM commercial.inventory_product_versions target
            JOIN commercial.inventory_product_versions peer
              ON peer.tenant_id = target.tenant_id AND peer.id <> target.id
            JOIN commercial.inventory_products product
              ON product.tenant_id = peer.tenant_id AND product.current_version_id = peer.id
            WHERE target.tenant_id = {tenantId.Value}
              AND target.id = ANY({targetProductVersionIds})
              AND target.spatial_location IS NOT NULL AND peer.spatial_location IS NOT NULL
              AND product.status_code = {MasterDataCodes.LifecycleStatuses.Active}
              AND ST_DWithin(peer.spatial_location, target.spatial_location,
                    {maximumRadiusMetres})
            ORDER BY target.id, "DistanceKilometres", peer.id
            """).ToListAsync(cancellationToken);
    }
}
