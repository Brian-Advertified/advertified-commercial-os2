using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202608300018_SelectedOptionBooking")]
public sealed partial class SelectedOptionBooking : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        CreateBookingTable(migrationBuilder);
        CreateBookingSecurityBoundary(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TABLE IF EXISTS commercial.bookings;
            DROP FUNCTION IF EXISTS commercial.enforce_booking_transition();
            ALTER TABLE commercial.proposal_decisions
                DROP CONSTRAINT IF EXISTS ux_proposal_decision_tenant_id;
            """);
    }
}
