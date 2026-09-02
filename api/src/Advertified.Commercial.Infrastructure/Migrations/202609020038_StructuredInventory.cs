using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609020038_StructuredInventory")]
public sealed partial class StructuredInventory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        CreateSupplierCommercialTables(migrationBuilder);
        CreatePackageAndAssetRightsTables(migrationBuilder);
        AddStructuredProductFields(migrationBuilder);
        CreateSpatialEvidenceTables(migrationBuilder);
        CreateSecurityBoundary(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP FUNCTION IF EXISTS commercial.read_approved_inventory_asset(uuid, date);
            DROP TABLE IF EXISTS commercial.inventory_asset_rights_reviews;
            DROP TABLE IF EXISTS commercial.inventory_package_components;
            DROP TABLE IF EXISTS commercial.inventory_packages;
            DROP TABLE IF EXISTS commercial.inventory_product_points_of_interest;
            DROP TABLE IF EXISTS commercial.inventory_supplier_contacts;
            ALTER TABLE commercial.inventory_suppliers
                DROP CONSTRAINT IF EXISTS fk_inventory_supplier_current_commercial_version,
                DROP COLUMN IF EXISTS current_commercial_version_id;
            DROP TABLE IF EXISTS commercial.inventory_supplier_versions;
            ALTER TABLE commercial.inventory_rates
                DROP CONSTRAINT IF EXISTS fk_inventory_rate_vat_treatment,
                DROP CONSTRAINT IF EXISTS ck_inventory_rate_vat_treatment_collection,
                DROP COLUMN IF EXISTS commercial_terms_json,
                DROP COLUMN IF EXISTS vat_treatment_code,
                DROP COLUMN IF EXISTS vat_treatment_collection_code;
            ALTER TABLE commercial.inventory_shortlist_candidates
                DROP CONSTRAINT IF EXISTS ck_shortlist_structured_inventory,
                DROP CONSTRAINT IF EXISTS ck_shortlist_commercial_readiness,
                DROP COLUMN IF EXISTS logo_asset_id,
                DROP COLUMN IF EXISTS commercial_terms_json,
                DROP COLUMN IF EXISTS supplier_commercial_json,
                DROP COLUMN IF EXISTS spatial_json,
                DROP COLUMN IF EXISTS deliverable_json,
                DROP COLUMN IF EXISTS commercial_readiness_json;
            ALTER TABLE commercial.marketplace_listing_versions
                DROP CONSTRAINT IF EXISTS ck_marketplace_structured_inventory,
                DROP CONSTRAINT IF EXISTS ck_marketplace_vat_collections,
                DROP CONSTRAINT IF EXISTS fk_marketplace_logo_asset,
                DROP CONSTRAINT IF EXISTS fk_marketplace_vat_treatment,
                DROP CONSTRAINT IF EXISTS fk_marketplace_supplier_vat_status,
                DROP COLUMN IF EXISTS logo_asset_id,
                DROP COLUMN IF EXISTS spatial_json,
                DROP COLUMN IF EXISTS deliverable_json,
                DROP COLUMN IF EXISTS commercial_terms_json,
                DROP COLUMN IF EXISTS supplier_commercial_json,
                DROP COLUMN IF EXISTS vat_treatment_code,
                DROP COLUMN IF EXISTS vat_treatment_collection_code,
                DROP COLUMN IF EXISTS supplier_vat_status_code,
                DROP COLUMN IF EXISTS supplier_vat_status_collection_code;
            ALTER TABLE commercial.bookings
                DROP CONSTRAINT IF EXISTS ck_booking_structured_inventory,
                DROP CONSTRAINT IF EXISTS fk_booking_logo_asset,
                DROP CONSTRAINT IF EXISTS fk_booking_vat_treatment,
                DROP COLUMN IF EXISTS logo_asset_id,
                DROP COLUMN IF EXISTS spatial_json,
                DROP COLUMN IF EXISTS deliverable_json,
                DROP COLUMN IF EXISTS commercial_terms_json,
                DROP COLUMN IF EXISTS vat_treatment_code,
                DROP COLUMN IF EXISTS vat_treatment_collection_code,
                DROP COLUMN IF EXISTS supplier_commercial_json;
            ALTER TABLE commercial.bookings
                DROP CONSTRAINT IF EXISTS ck_booking_amounts,
                ADD CONSTRAINT ck_booking_amounts CHECK (
                    fees_minor = markup_minor + commission_minor + management_fee_minor
                    AND client_price_minor = supplier_cost_minor + fees_minor + vat_minor);
            ALTER TABLE commercial.media_plan_lines
                DROP CONSTRAINT IF EXISTS ck_media_plan_line_structured_inventory,
                DROP CONSTRAINT IF EXISTS fk_media_plan_line_logo_asset,
                DROP CONSTRAINT IF EXISTS fk_media_plan_line_vat_treatment,
                DROP COLUMN IF EXISTS logo_asset_id,
                DROP COLUMN IF EXISTS spatial_json,
                DROP COLUMN IF EXISTS deliverable_json,
                DROP COLUMN IF EXISTS commercial_terms_json,
                DROP COLUMN IF EXISTS vat_treatment_code,
                DROP COLUMN IF EXISTS vat_treatment_collection_code,
                DROP COLUMN IF EXISTS supplier_commercial_json;
            ALTER TABLE commercial.media_plan_versions
                DROP CONSTRAINT IF EXISTS fk_media_plan_commercial_policy,
                DROP COLUMN IF EXISTS commercial_policy_version_id;
            ALTER TABLE commercial.inventory_product_versions
                DROP COLUMN IF EXISTS direction_geometry,
                DROP COLUMN IF EXISTS route_geometry,
                DROP COLUMN IF EXISTS catchment_geometry,
                DROP COLUMN IF EXISTS coverage_geometry,
                DROP COLUMN IF EXISTS spatial_json,
                DROP COLUMN IF EXISTS deliverable_json,
                DROP COLUMN IF EXISTS description;
            ALTER TABLE commercial.inventory_assets
                DROP CONSTRAINT IF EXISTS ux_inventory_assets_tenant_id;
            """);
    }
}
