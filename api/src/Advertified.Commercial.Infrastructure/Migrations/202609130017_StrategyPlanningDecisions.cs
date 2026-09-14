using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609130017_StrategyPlanningDecisions")]
public sealed class StrategyPlanningDecisions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE commercial.media_mix_versions
            ADD COLUMN IF NOT EXISTS planning_impact_json jsonb NULL;

        COMMENT ON COLUMN commercial.media_mix_versions.planning_impact_json IS
            'Planner-entered pre-buy impact estimate with explicit source/methodology. Inventory-backed forecasts remain separate evidence.';
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE commercial.media_mix_versions
            DROP COLUMN IF EXISTS planning_impact_json;
        """);
}
