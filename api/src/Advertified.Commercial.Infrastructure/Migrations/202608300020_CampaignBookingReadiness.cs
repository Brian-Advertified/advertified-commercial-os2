using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202608300020_CampaignBookingReadiness")]
public sealed partial class CampaignBookingReadiness : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        CreateCampaignTable(migrationBuilder);
        CreateCampaignSecurityBoundary(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            DROP TRIGGER IF EXISTS enforce_booking_funding_order ON commercial.bookings;
            DROP FUNCTION IF EXISTS commercial.enforce_booking_funding_order();
            DROP TABLE IF EXISTS commercial.campaigns;
            DROP FUNCTION IF EXISTS commercial.enforce_campaign_transition();
            """);
}
