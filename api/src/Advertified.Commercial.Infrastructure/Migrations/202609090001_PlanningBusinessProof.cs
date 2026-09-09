using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609090001_PlanningBusinessProof")]
public sealed class PlanningBusinessProof : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
        ALTER TABLE commercial.inventory_shortlist_candidates
            ADD COLUMN supplier_id uuid,
            ADD COLUMN supplier_name character varying(500),
            ADD COLUMN latitude numeric,
            ADD COLUMN longitude numeric;

        ALTER TABLE commercial.inventory_shortlist_candidates
            ADD CONSTRAINT ck_shortlist_candidate_latitude
                CHECK (latitude IS NULL OR latitude BETWEEN -90 AND 90),
            ADD CONSTRAINT ck_shortlist_candidate_longitude
                CHECK (longitude IS NULL OR longitude BETWEEN -180 AND 180);

        UPDATE commercial.inventory_shortlist_candidates candidate
        SET supplier_id = product.supplier_id,
            supplier_name = supplier.name,
            latitude = version.latitude,
            longitude = version.longitude
        FROM commercial.inventory_products product
        JOIN commercial.inventory_suppliers supplier
          ON supplier.tenant_id = product.tenant_id
         AND supplier.id = product.supplier_id
        JOIN commercial.inventory_product_versions version
          ON version.tenant_id = product.tenant_id
         AND version.id = product.current_version_id
        WHERE candidate.marketplace_listing_version_id IS NULL
          AND candidate.inventory_tenant_id = product.tenant_id
          AND candidate.inventory_product_id = product.id
          AND candidate.product_version_id = version.id;

        UPDATE commercial.inventory_shortlist_candidates candidate
        SET supplier_id = snapshot.supplier_id,
            supplier_name = snapshot.supplier_name
        FROM commercial.marketplace_listing_versions snapshot
        WHERE candidate.marketplace_listing_version_id = snapshot.id
          AND candidate.inventory_tenant_id = snapshot.supplier_tenant_id;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
        ALTER TABLE commercial.inventory_shortlist_candidates
            DROP CONSTRAINT ck_shortlist_candidate_longitude,
            DROP CONSTRAINT ck_shortlist_candidate_latitude,
            DROP COLUMN longitude,
            DROP COLUMN latitude,
            DROP COLUMN supplier_name,
            DROP COLUMN supplier_id;
        """);
}
