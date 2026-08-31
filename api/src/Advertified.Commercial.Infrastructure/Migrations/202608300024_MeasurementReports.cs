using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202608300024_MeasurementReports")]
public sealed partial class MeasurementReports : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        CreateMeasurementReportTables(migrationBuilder);
        CreateMeasurementReportSecurityBoundary(migrationBuilder);
        CreateMeasurementReportTaskBoundary(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        GuardMeasurementReportRollback(migrationBuilder);
        DropMeasurementReportBoundary(migrationBuilder);
    }
}
