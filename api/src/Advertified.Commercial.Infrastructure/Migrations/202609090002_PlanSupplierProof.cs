using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609090002_PlanSupplierProof")]
public sealed class PlanSupplierProof : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
        ALTER TABLE commercial.media_plan_lines
            ADD COLUMN supplier_name character varying(500);

        UPDATE commercial.media_plan_lines line
        SET supplier_name = candidate.supplier_name
        FROM commercial.inventory_shortlist_candidates candidate
        WHERE candidate.tenant_id = line.tenant_id
          AND candidate.id = line.shortlist_candidate_id
          AND candidate.supplier_name IS NOT NULL;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
        ALTER TABLE commercial.media_plan_lines
            DROP COLUMN supplier_name;
        """);
}
