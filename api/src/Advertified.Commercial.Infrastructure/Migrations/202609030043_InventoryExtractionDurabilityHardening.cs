using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609030043_InventoryExtractionDurabilityHardening")]
public sealed partial class InventoryExtractionDurabilityHardening : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        AddFailureStateConstraint(migrationBuilder);
        ReplaceTransitionGuard(migrationBuilder, HardenedTransitionGuardSql);
        ReplaceClaimFunction(migrationBuilder, BackedOffClaimFunctionSql);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE commercial.inventory_extraction_attempts
                DROP CONSTRAINT IF EXISTS ck_inventory_extraction_failure_state;
            """);
        ReplaceTransitionGuard(migrationBuilder, OriginalTransitionGuardSql);
        ReplaceClaimFunction(migrationBuilder, OriginalClaimFunctionSql);
    }
}
