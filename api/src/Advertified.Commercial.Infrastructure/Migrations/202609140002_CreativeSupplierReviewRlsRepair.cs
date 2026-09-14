using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609140002_CreativeSupplierReviewRlsRepair")]
public sealed class CreativeSupplierReviewRlsRepair : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RepairFunction);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Creative supplier review RLS repair is forward-only.");

    private const string RepairFunction = """
        CREATE OR REPLACE FUNCTION commercial.enforce_creative_asset_review() RETURNS trigger
            LANGUAGE plpgsql SECURITY DEFINER
            SET search_path TO 'pg_catalog', 'commercial'
        AS $$
        DECLARE current_version uuid;
        BEGIN
            IF TG_OP <> 'INSERT' THEN
                RAISE EXCEPTION 'creative reviews are immutable';
            END IF;

            SELECT asset.current_version_id
            INTO current_version
            FROM commercial.creative_assets asset
            WHERE asset.buyer_tenant_id = NEW.buyer_tenant_id
              AND asset.supplier_tenant_id = NEW.supplier_tenant_id
              AND asset.id = NEW.asset_id;

            IF NOT FOUND
               OR NEW.reviewed_by <> commercial.current_user_id()
               OR NEW.reviewer_tenant_id <> commercial.current_tenant_id()
               OR NEW.asset_version_id <> current_version
               OR (NEW.review_type_code = 'BRAND_LEGAL_RIGHTS'
                   AND NEW.buyer_tenant_id <> commercial.current_tenant_id())
               OR (NEW.review_type_code = 'SUPPLIER_TECHNICAL'
                   AND NEW.supplier_tenant_id <> commercial.current_tenant_id()) THEN
                RAISE EXCEPTION 'creative review is not authorised for the current version';
            END IF;
            RETURN NEW;
        END;
        $$;
        REVOKE ALL ON FUNCTION commercial.enforce_creative_asset_review() FROM PUBLIC;
        """;
}
