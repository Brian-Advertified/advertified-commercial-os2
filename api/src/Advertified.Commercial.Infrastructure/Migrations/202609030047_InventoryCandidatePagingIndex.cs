using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609030047_InventoryCandidatePagingIndex")]
public sealed class InventoryCandidatePagingIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP INDEX commercial.ix_inventory_candidates_import_page;
            CREATE INDEX ix_inventory_candidates_import_page
                ON commercial.inventory_candidates (
                    tenant_id, import_id, row_number, id)
                INCLUDE (status_code, reviewed_by, version, updated_at_utc);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP INDEX commercial.ix_inventory_candidates_import_page;
            CREATE INDEX ix_inventory_candidates_import_page
                ON commercial.inventory_candidates (
                    tenant_id, import_id, row_number, id)
                INCLUDE (
                    status_code, canonical_values_json, validation_json,
                    source_locator, reviewed_by, version, updated_at_utc);
            """);
    }
}
