using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202608290002_CanonicalCommercialFoundation")]
public sealed partial class CanonicalCommercialFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema("commercial");
        CreateIdentityTables(migrationBuilder);
        CreateTenantOwnedTables(migrationBuilder);
        CreatePlatformTables(migrationBuilder);
        CreateSecurityBoundary(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP SCHEMA IF EXISTS commercial CASCADE;");
    }
}
