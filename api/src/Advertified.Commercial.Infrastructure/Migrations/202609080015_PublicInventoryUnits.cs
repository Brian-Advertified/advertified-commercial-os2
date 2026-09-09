using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609080015_PublicInventoryUnits")]
public sealed class PublicInventoryUnits : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE commercial.public_inventory_listing_directory
            ADD COLUMN product_id uuid,
            ADD COLUMN product_name character varying(1000);
        ALTER TABLE commercial.marketplace_listings NO FORCE ROW LEVEL SECURITY;
        ALTER TABLE commercial.marketplace_listing_versions NO FORCE ROW LEVEL SECURITY;
        UPDATE commercial.public_inventory_listing_directory directory
        SET product_id = listing.product_id, product_name = snapshot.product_name
        FROM commercial.marketplace_listings listing
        JOIN commercial.marketplace_listing_versions snapshot
          ON snapshot.supplier_tenant_id = listing.supplier_tenant_id
         AND snapshot.id = listing.current_version_id
        WHERE directory.listing_id = listing.id;
        ALTER TABLE commercial.marketplace_listing_versions FORCE ROW LEVEL SECURITY;
        ALTER TABLE commercial.marketplace_listings FORCE ROW LEVEL SECURITY;
        ALTER TABLE commercial.public_inventory_listing_directory
            ALTER COLUMN product_id SET NOT NULL,
            ALTER COLUMN product_name SET NOT NULL;
        CREATE OR REPLACE FUNCTION commercial.sync_public_inventory_listing()
        RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER
        SET search_path = pg_catalog, commercial AS $function$
        BEGIN
            DELETE FROM commercial.public_inventory_listing_directory WHERE listing_id = NEW.id;
            IF NEW.status_code = 'PUBLISHED' AND NEW.soft_deleted_at_utc IS NULL THEN
                INSERT INTO commercial.public_inventory_listing_directory
                    (listing_id, channel_code, supplier_id, supplier_name, product_id, product_name)
                SELECT NEW.id, snapshot.channel_code, snapshot.supplier_id, snapshot.supplier_name,
                       NEW.product_id, snapshot.product_name
                FROM commercial.marketplace_listing_versions snapshot
                WHERE snapshot.supplier_tenant_id = NEW.supplier_tenant_id
                  AND snapshot.id = NEW.current_version_id;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'Published Marketplace listing has no readable current version.'
                        USING ERRCODE = '23514';
                END IF;
            END IF;
            RETURN NEW;
        END;
        $function$;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Public directory identity enrichment is forward-only.");
}
