using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609050004_InventoryRateVariants")]
public sealed class InventoryRateVariants : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            ALTER TABLE commercial.inventory_rates
                ADD COLUMN variant_json jsonb
                CHECK (variant_json IS NULL OR jsonb_typeof(variant_json) = 'object');
            """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF EXISTS (
                    SELECT 1 FROM commercial.inventory_rates
                    WHERE variant_json IS NOT NULL) THEN
                    RAISE EXCEPTION 'Retained rate variants must not be discarded';
                END IF;
            END $$;
            ALTER TABLE commercial.inventory_rates DROP COLUMN variant_json;
            """);
}
