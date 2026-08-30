using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202608300013_SupplierMarketplace")]
public sealed partial class SupplierMarketplace : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        CreateMarketplaceTables(migrationBuilder);
        CreateMarketplaceSecurityBoundary(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP POLICY IF EXISTS marketplace_listing_versions_read
                ON commercial.marketplace_listing_versions;
            DROP TABLE IF EXISTS commercial.marketplace_response_acceptances;
            DROP TABLE IF EXISTS commercial.marketplace_supplier_responses;
            DROP TABLE IF EXISTS commercial.marketplace_rfqs;
            ALTER TABLE commercial.marketplace_listings
                DROP CONSTRAINT IF EXISTS fk_marketplace_listing_current_version;
            DROP TABLE IF EXISTS commercial.marketplace_listing_versions;
            DROP TABLE IF EXISTS commercial.marketplace_listings;
            """);
    }
}
