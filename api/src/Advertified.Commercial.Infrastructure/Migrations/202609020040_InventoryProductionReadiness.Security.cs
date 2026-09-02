using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class InventoryProductionReadiness
{
    private static readonly string[] NewTables =
    [
        "brief_spatial_requirements",
        "inventory_availability_exceptions",
        "inventory_embedding_jobs",
    ];

    private static void AddInventoryProductionSecurity(MigrationBuilder migrationBuilder)
    {
        foreach (var table in NewTables)
        {
            migrationBuilder.Sql($$"""
                ALTER TABLE commercial.{{table}} ENABLE ROW LEVEL SECURITY;
                ALTER TABLE commercial.{{table}} FORCE ROW LEVEL SECURITY;
                CREATE POLICY {{table}}_tenant_scope ON commercial.{{table}}
                    USING (tenant_id = commercial.current_tenant_id())
                    WITH CHECK (tenant_id = commercial.current_tenant_id());
                """);
        }
        migrationBuilder.Sql(
            """
            CREATE TRIGGER protect_brief_spatial_requirements
                BEFORE UPDATE OR DELETE ON commercial.brief_spatial_requirements
                FOR EACH ROW EXECUTE FUNCTION commercial.reject_immutable_record_change();
            CREATE TRIGGER protect_inventory_availability_exceptions
                BEFORE UPDATE OR DELETE ON commercial.inventory_availability_exceptions
                FOR EACH ROW EXECUTE FUNCTION commercial.reject_immutable_record_change();
            CREATE TRIGGER protect_inventory_embedding_jobs
                BEFORE UPDATE OR DELETE ON commercial.inventory_embedding_jobs
                FOR EACH ROW EXECUTE FUNCTION commercial.reject_immutable_record_change();

            GRANT SELECT, INSERT ON commercial.brief_spatial_requirements TO advertified_app;
            GRANT SELECT, INSERT ON commercial.inventory_availability_exceptions TO advertified_app;
            GRANT SELECT, INSERT ON commercial.inventory_embedding_jobs TO advertified_app;

            CREATE FUNCTION commercial.try_parse_geojson(p_geojson text)
            RETURNS geometry
            LANGUAGE plpgsql IMMUTABLE
            SET search_path = pg_catalog, public
            AS $try_parse_geojson$
            BEGIN
                RETURN public.ST_SetSRID(public.ST_GeomFromGeoJSON(p_geojson), 4326);
            EXCEPTION WHEN OTHERS THEN
                RETURN NULL;
            END;
            $try_parse_geojson$;
            REVOKE ALL ON FUNCTION commercial.try_parse_geojson(text) FROM PUBLIC;
            GRANT EXECUTE ON FUNCTION commercial.try_parse_geojson(text) TO advertified_app;

            CREATE FUNCTION commercial.inventory_coverage_qualifies(
                p_coverage geometry, p_target geometry, p_threshold numeric)
            RETURNS boolean
            LANGUAGE plpgsql IMMUTABLE
            SET search_path = pg_catalog, public
            AS $inventory_coverage_qualifies$
            DECLARE
                target_area double precision;
            BEGIN
                IF p_coverage IS NULL OR p_target IS NULL OR
                    NOT public.ST_IsValid(p_coverage) OR NOT public.ST_IsValid(p_target) THEN
                    RETURN false;
                END IF;
                target_area := public.ST_Area(p_target::geography);
                IF target_area <= 0 THEN RETURN false; END IF;
                RETURN public.ST_Area(
                    public.ST_Intersection(p_coverage, p_target)::geography) / target_area
                    >= COALESCE(p_threshold, 0.5);
            EXCEPTION WHEN OTHERS THEN
                RETURN false;
            END;
            $inventory_coverage_qualifies$;
            REVOKE ALL ON FUNCTION commercial.inventory_coverage_qualifies(
                geometry, geometry, numeric) FROM PUBLIC;
            GRANT EXECUTE ON FUNCTION commercial.inventory_coverage_qualifies(
                geometry, geometry, numeric) TO advertified_app;

            CREATE FUNCTION commercial.inventory_asset_rights_valid(
                p_asset_id uuid, p_scope varchar, p_territory varchar, p_today date)
            RETURNS boolean
            LANGUAGE sql STABLE SECURITY DEFINER
            SET search_path = pg_catalog, commercial
            AS $asset_rights_valid$
                SELECT EXISTS (
                    SELECT 1
                    FROM commercial.inventory_assets asset
                    JOIN LATERAL (
                        SELECT review.*
                        FROM commercial.inventory_asset_rights_reviews review
                        WHERE review.tenant_id = asset.tenant_id
                          AND review.asset_id = asset.id
                        ORDER BY review.asset_version DESC LIMIT 1) rights ON TRUE
                    WHERE asset.id = p_asset_id
                      AND rights.rights_status_code = 'APPROVED'
                      AND rights.scope_codes ? p_scope
                      AND rights.territory_code = p_territory
                      AND rights.effective_on <= p_today
                      AND (rights.until_revoked OR rights.licensed_until >= p_today)
                      AND (asset.tenant_id = commercial.current_tenant_id()
                        OR EXISTS (
                            SELECT 1
                            FROM commercial.marketplace_listing_versions snapshot
                            JOIN commercial.marketplace_listings listing
                              ON listing.supplier_tenant_id = snapshot.supplier_tenant_id
                             AND listing.current_version_id = snapshot.id
                            WHERE snapshot.supplier_tenant_id = asset.tenant_id
                              AND snapshot.logo_asset_id = asset.id
                              AND listing.status_code = 'PUBLISHED')));
            $asset_rights_valid$;
            REVOKE ALL ON FUNCTION commercial.inventory_asset_rights_valid(
                uuid, varchar, varchar, date) FROM PUBLIC;
            GRANT EXECUTE ON FUNCTION commercial.inventory_asset_rights_valid(
                uuid, varchar, varchar, date) TO advertified_app;

            CREATE FUNCTION commercial.marketplace_inventory_unavailable_periods(
                p_supplier_tenant_id uuid, p_product_id uuid)
            RETURNS jsonb
            LANGUAGE sql STABLE SECURITY DEFINER
            SET search_path = pg_catalog, commercial
            AS $marketplace_unavailable$
                SELECT COALESCE(jsonb_agg(jsonb_build_object(
                    'start', exception.starts_on, 'end', exception.ends_on,
                    'reason', exception.exception_type_code)
                    ORDER BY exception.starts_on, exception.ends_on, exception.id), '[]')
                FROM commercial.inventory_availability_exceptions exception
                WHERE exception.tenant_id = p_supplier_tenant_id
                  AND exception.product_id = p_product_id
                  AND (p_supplier_tenant_id = commercial.current_tenant_id()
                    OR EXISTS (
                        SELECT 1 FROM commercial.marketplace_listings listing
                        WHERE listing.supplier_tenant_id = p_supplier_tenant_id
                          AND listing.product_id = p_product_id
                          AND listing.status_code = 'PUBLISHED'));
            $marketplace_unavailable$;
            REVOKE ALL ON FUNCTION commercial.marketplace_inventory_unavailable_periods(
                uuid, uuid) FROM PUBLIC;
            GRANT EXECUTE ON FUNCTION commercial.marketplace_inventory_unavailable_periods(
                uuid, uuid) TO advertified_app;

            CREATE OR REPLACE FUNCTION commercial.read_approved_inventory_asset(
                p_asset_id uuid, p_today date)
            RETURNS TABLE (object_key varchar, media_type varchar, content_hash char)
            LANGUAGE sql SECURITY DEFINER
            SET search_path = pg_catalog, commercial
            AS $approved_asset$
                SELECT asset.object_key, asset.media_type, asset.content_hash
                FROM commercial.inventory_assets asset
                WHERE asset.id = p_asset_id
                  AND commercial.inventory_asset_rights_valid(
                      asset.id,
                      CASE WHEN asset.tenant_id = commercial.current_tenant_id()
                          THEN 'INTERNAL_PLANNING'
                          ELSE 'MARKETPLACE_DISPLAY' END,
                      'ZA', p_today);
            $approved_asset$;
            """);
    }
}
