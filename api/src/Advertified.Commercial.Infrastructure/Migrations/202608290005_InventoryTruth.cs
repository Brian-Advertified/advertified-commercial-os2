using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202608290005_InventoryTruth")]
public sealed partial class InventoryTruth : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        CreateInventoryIntakeTables(migrationBuilder);
        CreateInventoryProductTables(migrationBuilder);
        CreateInventorySecurityBoundary(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.inventory_products
                DROP CONSTRAINT IF EXISTS fk_inventory_products_current_version;
            DROP TABLE IF EXISTS commercial.inventory_assets;
            DROP TABLE IF EXISTS commercial.inventory_availability;
            DROP TABLE IF EXISTS commercial.inventory_rates;
            DROP TABLE IF EXISTS commercial.inventory_product_versions;
            DROP TABLE IF EXISTS commercial.inventory_products;
            DROP TABLE IF EXISTS commercial.inventory_review_decisions;
            DROP TABLE IF EXISTS commercial.inventory_candidate_fields;
            DROP TABLE IF EXISTS commercial.inventory_candidates;
            DROP TABLE IF EXISTS commercial.inventory_import_steps;
            DROP TABLE IF EXISTS commercial.inventory_imports;
            DROP TABLE IF EXISTS commercial.inventory_suppliers;
            """);
    }
}
