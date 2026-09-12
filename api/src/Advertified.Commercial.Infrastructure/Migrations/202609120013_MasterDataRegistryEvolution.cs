using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609120013_MasterDataRegistryEvolution")]
public sealed partial class MasterDataRegistryEvolution : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP INDEX IF EXISTS governance.ux_master_data_items_collection_sort;
        ALTER TABLE governance.master_data_items
            ADD CONSTRAINT ux_master_data_items_collection_sort
            UNIQUE (collection_code, sort_order)
            DEFERRABLE INITIALLY DEFERRED;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE governance.master_data_items
            DROP CONSTRAINT IF EXISTS ux_master_data_items_collection_sort;
        CREATE UNIQUE INDEX ux_master_data_items_collection_sort
            ON governance.master_data_items (collection_code, sort_order);
        """);
}
