using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609030042_InventoryExtractionDurability")]
public sealed partial class InventoryExtractionDurability : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        CreateAttemptTable(migrationBuilder);
        AddAttemptGuards(migrationBuilder);
        AddWorkerFunctions(migrationBuilder);
        AddAttemptSecurity(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TRIGGER IF EXISTS enforce_inventory_extraction_attempt_transition
                ON commercial.inventory_extraction_attempts;
            DROP FUNCTION IF EXISTS commercial.heartbeat_inventory_extraction_attempt(
                uuid, integer);
            DROP FUNCTION IF EXISTS commercial.claim_next_inventory_extraction_attempt(
                uuid, integer);
            DROP FUNCTION IF EXISTS commercial.enforce_inventory_extraction_attempt_transition();
            ALTER TABLE commercial.inventory_extractions
                DROP CONSTRAINT IF EXISTS fk_inventory_extractions_attempt,
                DROP CONSTRAINT IF EXISTS ux_inventory_extractions_attempt,
                DROP COLUMN IF EXISTS attempt_id,
                DROP COLUMN IF EXISTS source_file_version;
            DROP TABLE IF EXISTS commercial.inventory_extraction_attempts;
            ALTER TABLE commercial.inventory_extractions
                DROP CONSTRAINT IF EXISTS ux_inventory_extractions_tenant_id;
            """);
    }
}
