using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609030044_InventoryExtractionArtifactFence")]
public sealed class InventoryExtractionArtifactFence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE FUNCTION commercial.enforce_current_inventory_extraction_artifact()
            RETURNS trigger
            LANGUAGE plpgsql
            SET search_path = pg_catalog, commercial
            AS $artifact_fence$
            BEGIN
                IF NEW.attempt_id IS NULL THEN
                    RETURN NEW;
                END IF;

                PERFORM 1
                FROM commercial.inventory_extraction_attempts attempt
                WHERE attempt.tenant_id = NEW.tenant_id
                  AND attempt.id = NEW.attempt_id
                  AND attempt.import_id = NEW.import_id
                  AND attempt.source_hash = NEW.source_hash
                  AND attempt.source_file_version = NEW.source_file_version
                  AND attempt.status_code = 'RUNNING'
                  AND attempt.extracted_artifact_id IS NULL
                  AND attempt.worker_lease_token IS NOT NULL
                  AND attempt.worker_lease_expires_at_utc > pg_catalog.statement_timestamp()
                  AND NOT EXISTS (
                      SELECT 1
                      FROM commercial.inventory_extraction_attempts newer
                      WHERE newer.tenant_id = attempt.tenant_id
                        AND newer.import_id = attempt.import_id
                        AND newer.attempt_number > attempt.attempt_number)
                FOR UPDATE;

                IF NOT FOUND THEN
                    RAISE EXCEPTION
                        'extraction artifact must belong to the current leased running attempt'
                        USING ERRCODE = '23514';
                END IF;
                RETURN NEW;
            END;
            $artifact_fence$;

            CREATE TRIGGER enforce_current_inventory_extraction_artifact
                BEFORE INSERT ON commercial.inventory_extractions
                FOR EACH ROW EXECUTE FUNCTION
                    commercial.enforce_current_inventory_extraction_artifact();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS enforce_current_inventory_extraction_artifact
                ON commercial.inventory_extractions;
            DROP FUNCTION IF EXISTS commercial.enforce_current_inventory_extraction_artifact();
            """);
    }
}
