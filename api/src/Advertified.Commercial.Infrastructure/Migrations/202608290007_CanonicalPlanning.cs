using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202608290007_CanonicalPlanning")]
public sealed partial class CanonicalPlanning : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        CreateAudienceAndMixTables(migrationBuilder);
        CreateShortlistTables(migrationBuilder);
        CreateMediaPlanTables(migrationBuilder);
        CreatePlanningSecurityBoundary(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TABLE IF EXISTS commercial.planning_objection_resolutions;
            DROP TABLE IF EXISTS commercial.supply_coordination;
            DROP TABLE IF EXISTS commercial.recommendation_bindings;
            DROP TABLE IF EXISTS commercial.media_plan_lines;
            DROP TABLE IF EXISTS commercial.media_plan_versions;
            DROP TABLE IF EXISTS commercial.shortlist_selections;
            DROP TABLE IF EXISTS commercial.inventory_benchmark_snapshots;
            DROP TABLE IF EXISTS commercial.inventory_shortlist_candidates;
            DROP TABLE IF EXISTS commercial.inventory_shortlist_versions;
            DROP TABLE IF EXISTS commercial.media_mix_versions;
            DROP TABLE IF EXISTS commercial.audience_definitions;
            DROP TABLE IF EXISTS commercial.audience_definition_sets;
            DROP INDEX IF EXISTS commercial.ix_inventory_product_versions_spatial_location;
            ALTER TABLE commercial.inventory_product_versions
                DROP COLUMN IF EXISTS spatial_location;
            ALTER TABLE commercial.inventory_availability
                DROP CONSTRAINT IF EXISTS ux_inventory_availability_tenant_id;
            ALTER TABLE commercial.inventory_rates
                DROP CONSTRAINT IF EXISTS ux_inventory_rates_tenant_id;
            """);
    }
}
