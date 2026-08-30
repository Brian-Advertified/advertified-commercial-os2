using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202608300015_InventoryQueryHardening")]
public sealed class InventoryQueryHardening : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE EXTENSION IF NOT EXISTS pg_trgm;

            CREATE INDEX ix_inventory_candidates_import_page
                ON commercial.inventory_candidates (
                    tenant_id, import_id, row_number, id)
                INCLUDE (
                    status_code, canonical_values_json, validation_json,
                    source_locator, reviewed_by, version, updated_at_utc);
            CREATE INDEX ix_inventory_candidates_validation
                ON commercial.inventory_candidates
                USING gin (validation_json jsonb_path_ops);

            CREATE INDEX ix_inventory_rates_latest
                ON commercial.inventory_rates (
                    tenant_id, product_version_id,
                    effective_from DESC NULLS LAST, id DESC)
                INCLUDE (rate_type_code, currency_code, amount_minor, source_locator);
            CREATE INDEX ix_inventory_availability_latest
                ON commercial.inventory_availability (
                    tenant_id, product_version_id,
                    observed_at_utc DESC NULLS LAST, id DESC)
                INCLUDE (availability_code, valid_until_utc, source_locator);

            CREATE INDEX ix_inventory_versions_name_search
                ON commercial.inventory_product_versions
                USING gin (name gin_trgm_ops);
            CREATE INDEX ix_inventory_versions_geography_search
                ON commercial.inventory_product_versions
                USING gin (geography gin_trgm_ops);
            CREATE INDEX ix_inventory_products_code_search
                ON commercial.inventory_products
                USING gin (supplier_product_code gin_trgm_ops);
            CREATE INDEX ix_inventory_suppliers_name_search
                ON commercial.inventory_suppliers
                USING gin (name gin_trgm_ops);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP INDEX IF EXISTS commercial.ix_inventory_suppliers_name_search;
            DROP INDEX IF EXISTS commercial.ix_inventory_products_code_search;
            DROP INDEX IF EXISTS commercial.ix_inventory_versions_geography_search;
            DROP INDEX IF EXISTS commercial.ix_inventory_versions_name_search;
            DROP INDEX IF EXISTS commercial.ix_inventory_availability_latest;
            DROP INDEX IF EXISTS commercial.ix_inventory_rates_latest;
            DROP INDEX IF EXISTS commercial.ix_inventory_candidates_validation;
            DROP INDEX IF EXISTS commercial.ix_inventory_candidates_import_page;
            """);
    }
}
