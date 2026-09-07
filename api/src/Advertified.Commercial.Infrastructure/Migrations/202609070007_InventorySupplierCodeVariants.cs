using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609070007_InventorySupplierCodeVariants")]
public sealed class InventorySupplierCodeVariants : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            ALTER TABLE commercial.inventory_products
                DROP CONSTRAINT ux_inventory_products_supplier_code;
            CREATE INDEX ix_inventory_products_supplier_code
                ON commercial.inventory_products (tenant_id, supplier_id, supplier_product_code);
            """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF EXISTS (
                    SELECT 1 FROM commercial.inventory_products
                    GROUP BY tenant_id, supplier_id, supplier_product_code
                    HAVING count(*) > 1) THEN
                    RAISE EXCEPTION 'Distinct supplier product variants share a source code';
                END IF;
            END $$;
            DROP INDEX commercial.ix_inventory_products_supplier_code;
            ALTER TABLE commercial.inventory_products
                ADD CONSTRAINT ux_inventory_products_supplier_code
                UNIQUE (tenant_id, supplier_id, supplier_product_code);
            """);
}
