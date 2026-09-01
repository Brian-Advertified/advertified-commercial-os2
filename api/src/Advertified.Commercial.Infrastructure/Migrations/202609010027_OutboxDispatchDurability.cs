using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609010027_OutboxDispatchDurability")]
public sealed partial class OutboxDispatchDurability : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        AddDispatchState(migrationBuilder);
        AddDispatchFunctions(migrationBuilder);
        AddDispatchTransitions(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        GuardRollback(migrationBuilder);
        RemoveDispatchFunctions(migrationBuilder);
        RemoveDispatchState(migrationBuilder);
    }
}
