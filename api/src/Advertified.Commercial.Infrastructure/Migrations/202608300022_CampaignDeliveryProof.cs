using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202608300022_CampaignDeliveryProof")]
public sealed partial class CampaignDeliveryProof : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        AddCampaignDeliveryState(migrationBuilder);
        CreateDeliveryProofTable(migrationBuilder);
        CreateDeliveryProofSecurityBoundary(migrationBuilder);
        CreateDeliveryProofTaskBoundary(migrationBuilder);
        ReplaceDeliveryCampaignTransitionBoundary(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        GuardDeliveryRollback(migrationBuilder);
        DropDeliveryProofBoundary(migrationBuilder);
        CreativeProductionReadiness.ReplaceCampaignTransitionBoundary(migrationBuilder);
    }
}
