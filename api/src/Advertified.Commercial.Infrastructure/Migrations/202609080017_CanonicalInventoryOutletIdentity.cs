using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609080017_CanonicalInventoryOutletIdentity")]
public sealed class CanonicalInventoryOutletIdentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE commercial.inventory_product_versions
            ADD COLUMN outlet_id character varying(64),
            ADD COLUMN outlet_name character varying(500),
            ADD COLUMN outlet_identity_basis character varying(50),
            ADD COLUMN outlet_source_locator character varying(1000);
        ALTER TABLE commercial.marketplace_listing_versions
            ADD COLUMN outlet_id character varying(64),
            ADD COLUMN outlet_name character varying(500),
            ADD COLUMN outlet_identity_basis character varying(50),
            ADD COLUMN outlet_source_locator character varying(1000);
        ALTER TABLE commercial.public_inventory_listing_directory
            ADD COLUMN outlet_id character varying(64),
            ADD COLUMN outlet_name character varying(500),
            ADD COLUMN outlet_identity_basis character varying(50);

        ALTER TABLE commercial.inventory_product_versions NO FORCE ROW LEVEL SECURITY;
        ALTER TABLE commercial.inventory_candidates NO FORCE ROW LEVEL SECURITY;
        ALTER TABLE commercial.marketplace_listings NO FORCE ROW LEVEL SECURITY;
        ALTER TABLE commercial.marketplace_listing_versions NO FORCE ROW LEVEL SECURITY;

        CREATE FUNCTION commercial.legacy_inventory_outlet_name(p_channel text, p_name text)
        RETURNS text LANGUAGE plpgsql IMMUTABLE SET search_path = pg_catalog AS $function$
        DECLARE resolved text;
        BEGIN
            IF p_channel NOT IN ('RADIO', 'TV', 'PRINT') OR p_name IS NULL OR btrim(p_name) = '' THEN
                RETURN NULL;
            END IF;
            resolved := btrim(p_name);
            IF p_channel <> 'PRINT' AND resolved ~* '(package|simulcast|banner|cpm|cpv|\\+)' THEN
                RETURN NULL;
            END IF;
            IF position(' — ' in resolved) > 0 THEN
                resolved := split_part(resolved, ' — ', 1);
            ELSIF resolved ~* '\\s+(MONDAY([-_]FRIDAY)?|SATURDAY|SUNDAY)\\s+\\d' THEN
                resolved := regexp_replace(resolved,
                    '\\s+(MONDAY([-_]FRIDAY)?|SATURDAY|SUNDAY)\\s+\\d.*$', '', 'i');
            ELSIF resolved ~* '\\s+\\d+[- ]second\\s+(spot|commercial)$' THEN
                resolved := regexp_replace(resolved,
                    '\\s+\\d+[- ]second\\s+(spot|commercial)$', '', 'i');
            ELSE
                RETURN NULL;
            END IF;
            resolved := btrim(regexp_replace(resolved,
                '\\s+\\d+[- ]second\\s+(spot|commercial)$', '', 'i'));
            IF p_channel = 'TV' THEN
                resolved := btrim(regexp_replace(resolved, '\\s+channel\\s+\\d+$', '', 'i'));
            END IF;
            IF resolved = '' OR position(',' in resolved) > 0 OR length(resolved) > 500 THEN
                RETURN NULL;
            END IF;
            RETURN resolved;
        END $function$;

        ALTER TABLE commercial.inventory_product_versions
            DISABLE TRIGGER protect_inventory_product_versions;
        ALTER TABLE commercial.marketplace_listing_versions
            DISABLE TRIGGER protect_marketplace_listing_versions;

        UPDATE commercial.inventory_product_versions version
        SET outlet_name = commercial.legacy_inventory_outlet_name(
                version.channel_code, version.name),
            outlet_id = upper(encode(digest(
                version.channel_code || ':' || upper(commercial.legacy_inventory_outlet_name(
                    version.channel_code, version.name)), 'sha256'), 'hex')),
            outlet_identity_basis = 'LEGACY_SOURCE_TITLE_PATTERN',
            outlet_source_locator = (
                SELECT candidate.source_locator
                FROM commercial.inventory_candidates candidate
                WHERE candidate.tenant_id = version.tenant_id
                  AND candidate.id = version.source_candidate_id)
        WHERE commercial.legacy_inventory_outlet_name(
            version.channel_code, version.name) IS NOT NULL;

        UPDATE commercial.marketplace_listing_versions listing
        SET outlet_id = version.outlet_id,
            outlet_name = version.outlet_name,
            outlet_identity_basis = version.outlet_identity_basis,
            outlet_source_locator = version.outlet_source_locator
        FROM commercial.inventory_product_versions version
        WHERE version.tenant_id = listing.supplier_tenant_id
          AND version.id = listing.product_version_id;

        ALTER TABLE commercial.marketplace_listing_versions
            ENABLE TRIGGER protect_marketplace_listing_versions;
        ALTER TABLE commercial.inventory_product_versions
            ENABLE TRIGGER protect_inventory_product_versions;

        UPDATE commercial.public_inventory_listing_directory directory
        SET outlet_id = listing.outlet_id,
            outlet_name = listing.outlet_name,
            outlet_identity_basis = listing.outlet_identity_basis
        FROM commercial.marketplace_listings current
        JOIN commercial.marketplace_listing_versions listing
          ON listing.supplier_tenant_id = current.supplier_tenant_id
         AND listing.id = current.current_version_id
        WHERE directory.listing_id = current.id;

        ALTER TABLE commercial.marketplace_listing_versions FORCE ROW LEVEL SECURITY;
        ALTER TABLE commercial.marketplace_listings FORCE ROW LEVEL SECURITY;
        ALTER TABLE commercial.inventory_candidates FORCE ROW LEVEL SECURITY;
        ALTER TABLE commercial.inventory_product_versions FORCE ROW LEVEL SECURITY;

        CREATE OR REPLACE FUNCTION commercial.sync_public_inventory_listing()
        RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER
        SET search_path = pg_catalog, commercial AS $function$
        BEGIN
            DELETE FROM commercial.public_inventory_listing_directory WHERE listing_id = NEW.id;
            IF NEW.status_code = 'PUBLISHED' AND NEW.soft_deleted_at_utc IS NULL THEN
                INSERT INTO commercial.public_inventory_listing_directory
                    (listing_id, channel_code, supplier_id, supplier_name, product_id, product_name,
                     outlet_id, outlet_name, outlet_identity_basis)
                SELECT NEW.id, snapshot.channel_code, snapshot.supplier_id, snapshot.supplier_name,
                       NEW.product_id, snapshot.product_name, snapshot.outlet_id,
                       snapshot.outlet_name, snapshot.outlet_identity_basis
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

        DROP FUNCTION commercial.legacy_inventory_outlet_name(text, text);
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Canonical inventory outlet identity is forward-only.");
}
