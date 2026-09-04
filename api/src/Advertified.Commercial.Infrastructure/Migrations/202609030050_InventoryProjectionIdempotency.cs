using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609030050_InventoryProjectionIdempotency")]
public sealed class InventoryProjectionIdempotency : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE UNIQUE INDEX
                ux_inventory_projection_input_version
                ON commercial.inventory_extraction_projections (
                    tenant_id, input_artifact_id,
                    projector_code, projector_version);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP INDEX IF EXISTS
                commercial.ux_inventory_projection_input_version;
            """);
    }
}
