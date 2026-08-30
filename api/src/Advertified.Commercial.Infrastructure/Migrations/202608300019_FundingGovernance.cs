using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Advertified.Commercial.Infrastructure.MasterData;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202608300019_FundingGovernance")]
public sealed partial class FundingGovernance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        CreateFundingTables(migrationBuilder);
        CreateFundingSecurityBoundary(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            DROP TABLE IF EXISTS commercial.payment_intents;
            DROP TABLE IF EXISTS commercial.invoices;
            DROP TABLE IF EXISTS commercial.purchase_orders;
            DROP FUNCTION IF EXISTS commercial.enforce_payment_transition();
            DROP FUNCTION IF EXISTS commercial.enforce_invoice_integrity();
            DROP FUNCTION IF EXISTS commercial.enforce_purchase_order_transition();
            """);
}
