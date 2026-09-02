using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class InventoryProductionReadiness
{
    private static void RestoreAssetRightsSecurity(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            CREATE OR REPLACE FUNCTION commercial.read_approved_inventory_asset(
                p_asset_id uuid, p_today date)
            RETURNS TABLE (object_key varchar, media_type varchar, content_hash char)
            LANGUAGE sql SECURITY DEFINER
            SET search_path = pg_catalog, commercial
            AS $approved_asset$
                SELECT asset.object_key, asset.media_type, asset.content_hash
                FROM commercial.inventory_assets asset
                JOIN LATERAL (
                    SELECT review.rights_status_code, review.licensed_until
                    FROM commercial.inventory_asset_rights_reviews review
                    WHERE review.tenant_id = asset.tenant_id
                      AND review.asset_id = asset.id
                    ORDER BY review.asset_version DESC LIMIT 1) rights ON TRUE
                WHERE asset.id = p_asset_id
                  AND rights.rights_status_code = 'APPROVED'
                  AND (rights.licensed_until IS NULL OR rights.licensed_until >= p_today)
                  AND (asset.tenant_id = commercial.current_tenant_id()
                    OR EXISTS (
                        SELECT 1
                        FROM commercial.marketplace_listing_versions snapshot
                        JOIN commercial.marketplace_listings listing
                          ON listing.supplier_tenant_id = snapshot.supplier_tenant_id
                         AND listing.current_version_id = snapshot.id
                        WHERE snapshot.supplier_tenant_id = asset.tenant_id
                          AND snapshot.logo_asset_id = asset.id
                          AND listing.status_code = 'PUBLISHED'));
            $approved_asset$;
            DROP FUNCTION IF EXISTS commercial.inventory_asset_rights_valid(
                uuid, varchar, varchar, date);
            DROP FUNCTION IF EXISTS commercial.marketplace_inventory_unavailable_periods(
                uuid, uuid);
            DROP FUNCTION IF EXISTS commercial.inventory_coverage_qualifies(
                geometry, geometry, numeric);
            DROP FUNCTION IF EXISTS commercial.try_parse_geojson(text);
            """);
}
