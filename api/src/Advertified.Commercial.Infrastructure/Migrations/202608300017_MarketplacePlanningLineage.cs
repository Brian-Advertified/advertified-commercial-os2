using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202608300017_MarketplacePlanningLineage")]
public sealed partial class MarketplacePlanningLineage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ExtendMarketplaceSnapshots(migrationBuilder);
        ExtendShortlistLineage(migrationBuilder);
        ExtendRecommendationAndBenchmarkLineage(migrationBuilder);
        ExtendMediaPlanLineage(migrationBuilder);
        ExtendSupplyCoordinationLineage(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        EnsureLineageCanBeRemoved(migrationBuilder);
        RestoreSupplyCoordination(migrationBuilder);
        RestoreMediaPlanLines(migrationBuilder);
        RestoreRecommendationAndBenchmark(migrationBuilder);
        RestoreShortlistCandidates(migrationBuilder);
        RestoreMarketplaceSnapshots(migrationBuilder);
    }
}
