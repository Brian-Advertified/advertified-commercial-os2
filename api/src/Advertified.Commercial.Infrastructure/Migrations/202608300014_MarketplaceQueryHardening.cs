using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202608300014_MarketplaceQueryHardening")]
public sealed class MarketplaceQueryHardening : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE EXTENSION IF NOT EXISTS pg_trgm;

            DROP INDEX IF EXISTS commercial.ix_marketplace_listing_search;
            CREATE INDEX ix_marketplace_listing_published_page
                ON commercial.marketplace_listings (
                    status_code, updated_at_utc DESC, id DESC)
                INCLUDE (current_version_id, supplier_tenant_id);
            CREATE INDEX ix_marketplace_listing_version_channel
                ON commercial.marketplace_listing_versions (channel_code, id);
            CREATE INDEX ix_marketplace_listing_version_product_search
                ON commercial.marketplace_listing_versions
                USING gin (product_name gin_trgm_ops);
            CREATE INDEX ix_marketplace_listing_version_supplier_search
                ON commercial.marketplace_listing_versions
                USING gin (supplier_name gin_trgm_ops);
            CREATE INDEX ix_marketplace_listing_version_geography_search
                ON commercial.marketplace_listing_versions
                USING gin (geography gin_trgm_ops);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP INDEX IF EXISTS commercial.ix_marketplace_listing_version_geography_search;
            DROP INDEX IF EXISTS commercial.ix_marketplace_listing_version_supplier_search;
            DROP INDEX IF EXISTS commercial.ix_marketplace_listing_version_product_search;
            DROP INDEX IF EXISTS commercial.ix_marketplace_listing_version_channel;
            DROP INDEX IF EXISTS commercial.ix_marketplace_listing_published_page;
            CREATE INDEX ix_marketplace_listing_search
                ON commercial.marketplace_listing_versions (
                    lower(product_name), channel_code, geography, id);
            """);
    }
}
