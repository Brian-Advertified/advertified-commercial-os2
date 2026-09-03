using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class InventoryExtractionDurability
{
    private static void AddAttemptSecurity(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.inventory_extraction_attempts ENABLE ROW LEVEL SECURITY;
            ALTER TABLE commercial.inventory_extraction_attempts FORCE ROW LEVEL SECURITY;
            CREATE POLICY inventory_extraction_attempts_tenant_scope
                ON commercial.inventory_extraction_attempts
                USING (tenant_id = commercial.current_tenant_id())
                WITH CHECK (tenant_id = commercial.current_tenant_id());
            REVOKE ALL ON TABLE commercial.inventory_extraction_attempts FROM PUBLIC;
            REVOKE ALL ON TABLE commercial.inventory_extraction_attempts FROM advertified_worker;
            GRANT SELECT, INSERT, UPDATE ON commercial.inventory_extraction_attempts
                TO advertified_app;

            REVOKE ALL ON FUNCTION commercial.claim_next_inventory_extraction_attempt(
                uuid, integer) FROM PUBLIC;
            REVOKE ALL ON FUNCTION commercial.heartbeat_inventory_extraction_attempt(
                uuid, integer) FROM PUBLIC;
            GRANT EXECUTE ON FUNCTION commercial.claim_next_inventory_extraction_attempt(
                uuid, integer) TO advertified_worker;
            GRANT EXECUTE ON FUNCTION commercial.heartbeat_inventory_extraction_attempt(
                uuid, integer) TO advertified_worker;
            """);
    }
}
