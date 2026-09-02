using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609020036_CanonicalInventoryExtraction")]
public sealed class CanonicalInventoryExtraction : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.inventory_extractions
                RENAME COLUMN structured_json TO provider_json;
            ALTER TABLE commercial.inventory_extractions
                RENAME COLUMN output_hash TO provider_output_hash;
            ALTER TABLE commercial.inventory_extractions
                RENAME CONSTRAINT ck_inventory_extractions_output_hash
                TO ck_inventory_extractions_provider_output_hash;

            ALTER TABLE commercial.inventory_extractions
                ADD COLUMN canonical_json jsonb,
                ADD COLUMN canonical_output_hash char(64),
                ADD CONSTRAINT ck_inventory_extractions_canonical_output_hash CHECK (
                    canonical_output_hash IS NULL OR
                    canonical_output_hash ~ '^[0-9a-f]{64}$'),
                ADD CONSTRAINT ck_inventory_extractions_canonical_v2 CHECK (
                    schema_version <> 'advertified.inventory-extraction.v2'
                    OR (canonical_json IS NOT NULL AND canonical_output_hash IS NOT NULL));
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.inventory_extractions
                DROP CONSTRAINT IF EXISTS ck_inventory_extractions_canonical_v2,
                DROP CONSTRAINT IF EXISTS ck_inventory_extractions_canonical_output_hash,
                DROP COLUMN IF EXISTS canonical_output_hash,
                DROP COLUMN IF EXISTS canonical_json;
            ALTER TABLE commercial.inventory_extractions
                RENAME CONSTRAINT ck_inventory_extractions_provider_output_hash
                TO ck_inventory_extractions_output_hash;
            ALTER TABLE commercial.inventory_extractions
                RENAME COLUMN provider_output_hash TO output_hash;
            ALTER TABLE commercial.inventory_extractions
                RENAME COLUMN provider_json TO structured_json;
            """);
    }
}
