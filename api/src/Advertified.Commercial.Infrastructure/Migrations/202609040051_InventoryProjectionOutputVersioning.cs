using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609040051_InventoryProjectionOutputVersioning")]
public sealed class InventoryProjectionOutputVersioning : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.inventory_extraction_projections
                DROP CONSTRAINT ux_inventory_projection_output;

            CREATE INDEX ix_inventory_projection_output
                ON commercial.inventory_extraction_projections (
                    tenant_id, input_artifact_id,
                    canonical_output_hash);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP INDEX IF EXISTS
                commercial.ix_inventory_projection_output;

            ALTER TABLE commercial.inventory_extraction_projections
                ADD CONSTRAINT ux_inventory_projection_output
                UNIQUE (
                    tenant_id, input_artifact_id,
                    canonical_output_hash);
            """);
    }
}
