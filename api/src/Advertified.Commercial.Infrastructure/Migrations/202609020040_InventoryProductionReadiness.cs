using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609020040_InventoryProductionReadiness")]
public sealed partial class InventoryProductionReadiness : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        AddInventoryProductionTables(migrationBuilder);
        AddInventoryProductionSecurity(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        RestoreAssetRightsSecurity(migrationBuilder);
        migrationBuilder.Sql(
            """
        ALTER TABLE commercial.inventory_product_embeddings
            DROP CONSTRAINT IF EXISTS fk_inventory_embedding_job,
            DROP COLUMN IF EXISTS job_id;
        DROP TABLE IF EXISTS commercial.inventory_embedding_jobs;
        DROP TABLE IF EXISTS commercial.inventory_availability_exceptions;
        DROP TABLE IF EXISTS commercial.brief_spatial_requirements;

        DROP INDEX IF EXISTS commercial.ix_marketplace_listing_private_route;
        DROP INDEX IF EXISTS commercial.ix_marketplace_listing_private_catchment;
        DROP INDEX IF EXISTS commercial.ix_marketplace_listing_private_coverage;
        DROP INDEX IF EXISTS commercial.ix_marketplace_listing_private_location;
        ALTER TABLE commercial.marketplace_listing_versions
            DROP COLUMN IF EXISTS rate_source_locator,
            DROP COLUMN IF EXISTS availability_source_locator,
            DROP COLUMN IF EXISTS private_route_geometry,
            DROP COLUMN IF EXISTS private_catchment_geometry,
            DROP COLUMN IF EXISTS private_coverage_geometry,
            DROP COLUMN IF EXISTS private_spatial_location;

        ALTER TABLE commercial.inventory_asset_rights_reviews
            DROP COLUMN IF EXISTS scope_codes,
            DROP COLUMN IF EXISTS territory_code,
            DROP COLUMN IF EXISTS effective_on,
            DROP COLUMN IF EXISTS until_revoked,
            DROP COLUMN IF EXISTS attestor_role_code,
            DROP COLUMN IF EXISTS evidence_reference,
            DROP COLUMN IF EXISTS evidence_hash;
        ALTER TABLE commercial.inventory_shortlist_candidates
            DROP COLUMN IF EXISTS suitability_json,
            DROP COLUMN IF EXISTS spatial_match_json;
        ALTER TABLE commercial.audience_definitions
            DROP COLUMN IF EXISTS lsm_sem_mandatory;
        ALTER TABLE commercial.inventory_candidate_fields
            DROP COLUMN IF EXISTS evidence_basis_collection_code,
            DROP COLUMN IF EXISTS evidence_basis_code,
            DROP COLUMN IF EXISTS verification_state_collection_code,
            DROP COLUMN IF EXISTS verification_state_code,
            DROP COLUMN IF EXISTS required_action_collection_code,
            DROP COLUMN IF EXISTS required_action_code,
            DROP COLUMN IF EXISTS captured_at_utc,
            DROP COLUMN IF EXISTS effective_on,
            DROP COLUMN IF EXISTS fresh_until,
            DROP COLUMN IF EXISTS extraction_method_collection_code,
            DROP COLUMN IF EXISTS extraction_method_code,
            DROP COLUMN IF EXISTS extraction_confidence;
        """);
    }
}
