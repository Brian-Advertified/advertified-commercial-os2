using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class StructuredInventory
{
    private static readonly string[] StructuredInventoryTables =
    [
        "inventory_supplier_versions",
        "inventory_supplier_contacts",
        "inventory_packages",
        "inventory_package_components",
        "inventory_asset_rights_reviews",
        "inventory_product_points_of_interest",
    ];

    private static void CreateSecurityBoundary(MigrationBuilder migrationBuilder)
    {
        foreach (var table in StructuredInventoryTables)
        {
            migrationBuilder.Sql($"""
                ALTER TABLE commercial.{table} ENABLE ROW LEVEL SECURITY;
                ALTER TABLE commercial.{table} FORCE ROW LEVEL SECURITY;
                CREATE POLICY {table}_tenant_scope ON commercial.{table}
                    USING (tenant_id = commercial.current_tenant_id())
                    WITH CHECK (tenant_id = commercial.current_tenant_id());
                """);
        }
        migrationBuilder.Sql(
            """
            CREATE TRIGGER protect_inventory_supplier_versions
                BEFORE UPDATE OR DELETE ON commercial.inventory_supplier_versions
                FOR EACH ROW EXECUTE FUNCTION commercial.reject_immutable_record_change();
            CREATE TRIGGER protect_inventory_supplier_contacts
                BEFORE UPDATE OR DELETE ON commercial.inventory_supplier_contacts
                FOR EACH ROW EXECUTE FUNCTION commercial.reject_immutable_record_change();
            CREATE TRIGGER protect_inventory_packages
                BEFORE UPDATE OR DELETE ON commercial.inventory_packages
                FOR EACH ROW EXECUTE FUNCTION commercial.reject_immutable_record_change();
            CREATE TRIGGER protect_inventory_package_components
                BEFORE UPDATE OR DELETE ON commercial.inventory_package_components
                FOR EACH ROW EXECUTE FUNCTION commercial.reject_immutable_record_change();
            CREATE TRIGGER protect_inventory_asset_rights_reviews
                BEFORE UPDATE OR DELETE ON commercial.inventory_asset_rights_reviews
                FOR EACH ROW EXECUTE FUNCTION commercial.reject_immutable_record_change();
            CREATE TRIGGER protect_inventory_product_points_of_interest
                BEFORE UPDATE OR DELETE ON commercial.inventory_product_points_of_interest
                FOR EACH ROW EXECUTE FUNCTION commercial.reject_immutable_record_change();
            GRANT SELECT, INSERT, UPDATE ON
                commercial.inventory_supplier_versions,
                commercial.inventory_supplier_contacts,
                commercial.inventory_packages,
                commercial.inventory_package_components,
                commercial.inventory_asset_rights_reviews
                , commercial.inventory_product_points_of_interest
                TO advertified_app;

            CREATE FUNCTION commercial.read_approved_inventory_asset(
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
                          AND listing.status_code = 'PUBLISHED'
                          AND EXISTS (
                              SELECT 1 FROM commercial.memberships membership
                              WHERE membership.tenant_id = commercial.current_tenant_id()
                                AND membership.user_id = commercial.current_user_id()
                                AND membership.status_code = 'ACTIVE')));
            $approved_asset$;
            REVOKE ALL ON FUNCTION commercial.read_approved_inventory_asset(uuid, date)
                FROM PUBLIC;
            GRANT EXECUTE ON FUNCTION commercial.read_approved_inventory_asset(uuid, date)
                TO advertified_app;
            """);
    }
}
