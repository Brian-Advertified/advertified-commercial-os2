using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609080010_PublicInventorySummary")]
public sealed class PublicInventorySummary : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE commercial.public_inventory_listing_directory (
            listing_id uuid NOT NULL,
            channel_code character varying(100) NOT NULL,
            supplier_id uuid NOT NULL,
            supplier_name character varying(500) NOT NULL,
            CONSTRAINT pk_public_inventory_listing_directory PRIMARY KEY (listing_id),
            CONSTRAINT fk_public_inventory_listing_directory_listing
                FOREIGN KEY (listing_id) REFERENCES commercial.marketplace_listings(id)
                ON DELETE CASCADE
        );
        CREATE INDEX ix_public_inventory_directory_channel_owner
            ON commercial.public_inventory_listing_directory
                (channel_code, supplier_name, supplier_id);

        ALTER TABLE commercial.marketplace_listings NO FORCE ROW LEVEL SECURITY;
        ALTER TABLE commercial.marketplace_listing_versions NO FORCE ROW LEVEL SECURITY;
        INSERT INTO commercial.public_inventory_listing_directory
            (listing_id, channel_code, supplier_id, supplier_name)
        SELECT listing.id, snapshot.channel_code, snapshot.supplier_id,
               snapshot.supplier_name
        FROM commercial.marketplace_listings listing
        JOIN commercial.marketplace_listing_versions snapshot
          ON snapshot.supplier_tenant_id = listing.supplier_tenant_id
         AND snapshot.id = listing.current_version_id
        WHERE listing.status_code = 'PUBLISHED'
          AND listing.soft_deleted_at_utc IS NULL;
        ALTER TABLE commercial.marketplace_listing_versions FORCE ROW LEVEL SECURITY;
        ALTER TABLE commercial.marketplace_listings FORCE ROW LEVEL SECURITY;

        CREATE FUNCTION commercial.sync_public_inventory_listing()
        RETURNS trigger
        LANGUAGE plpgsql
        SECURITY DEFINER
        SET search_path = pg_catalog, commercial
        AS $function$
        BEGIN
            DELETE FROM commercial.public_inventory_listing_directory
            WHERE listing_id = NEW.id;
            IF NEW.status_code = 'PUBLISHED'
               AND NEW.soft_deleted_at_utc IS NULL THEN
                INSERT INTO commercial.public_inventory_listing_directory
                    (listing_id, channel_code, supplier_id, supplier_name)
                SELECT NEW.id, snapshot.channel_code, snapshot.supplier_id,
                       snapshot.supplier_name
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
        CREATE TRIGGER sync_public_inventory_listing
            AFTER INSERT OR UPDATE OF status_code, current_version_id, soft_deleted_at_utc
            ON commercial.marketplace_listings
            FOR EACH ROW EXECUTE FUNCTION commercial.sync_public_inventory_listing();
        GRANT SELECT ON commercial.public_inventory_listing_directory TO advertified_app;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TRIGGER sync_public_inventory_listing ON commercial.marketplace_listings;
        DROP FUNCTION commercial.sync_public_inventory_listing();
        DROP TABLE commercial.public_inventory_listing_directory;
        """);
}
