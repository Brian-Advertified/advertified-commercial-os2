using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202608300011_InventorySearchPagingIndex")]
public sealed class InventorySearchPagingIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE INDEX ix_inventory_products_current_lookup
                ON commercial.inventory_products (
                    tenant_id, current_version_id, id)
                INCLUDE (
                    supplier_id, supplier_product_code, status_code,
                    version, updated_at_utc);

            CREATE INDEX ix_inventory_versions_channel_name_cursor
                ON commercial.inventory_product_versions (
                    tenant_id, channel_code, lower(name), product_id)
                INCLUDE (id, product_type_code, geography, verification_code);

            CREATE INDEX ix_inventory_versions_name_cursor
                ON commercial.inventory_product_versions (
                    tenant_id, lower(name), product_id)
                INCLUDE (id, channel_code, product_type_code, geography, verification_code);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP INDEX IF EXISTS commercial.ix_inventory_versions_name_cursor;
            DROP INDEX IF EXISTS commercial.ix_inventory_versions_channel_name_cursor;
            DROP INDEX IF EXISTS commercial.ix_inventory_products_current_lookup;
            """);
    }
}
