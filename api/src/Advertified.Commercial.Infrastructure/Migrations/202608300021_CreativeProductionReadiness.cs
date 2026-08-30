using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202608300021_CreativeProductionReadiness")]
public sealed partial class CreativeProductionReadiness : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        AddCampaignCreativeState(migrationBuilder);
        CreateCreativeRequirementAndAssetTables(migrationBuilder);
        CreateCreativeVersionAndReviewTables(migrationBuilder);
        CreateCreativeSecurityBoundary(migrationBuilder);
        CreateCreativeValidationBoundary(migrationBuilder);
        ReplaceCampaignTransitionBoundary(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        RestoreBookingOnlyCampaignBoundary(migrationBuilder);
        DropCreativeBoundary(migrationBuilder);
    }
}
