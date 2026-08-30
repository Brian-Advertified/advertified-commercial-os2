using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202608300023_PerformanceEvidenceFacts")]
public sealed partial class PerformanceEvidenceFacts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        CreatePerformanceEvidenceTables(migrationBuilder);
        CreatePerformanceEvidenceSecurityBoundary(migrationBuilder);
        CreatePerformanceEvidenceTaskBoundary(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        GuardPerformanceEvidenceRollback(migrationBuilder);
        DropPerformanceEvidenceBoundary(migrationBuilder);
    }
}
