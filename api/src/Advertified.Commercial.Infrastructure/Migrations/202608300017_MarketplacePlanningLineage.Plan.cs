using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class MarketplacePlanningLineage
{
    private static void ExtendMediaPlanLineage(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.media_plan_lines
                ADD COLUMN inventory_tenant_id uuid,
                ADD COLUMN marketplace_listing_version_id uuid,
                ADD COLUMN product_name varchar(500),
                ADD COLUMN channel_code varchar(100),
                ADD COLUMN geography varchar(500);
            ALTER TABLE commercial.media_plan_lines
                DISABLE TRIGGER protect_media_plan_lines;
            UPDATE commercial.media_plan_lines line
            SET inventory_tenant_id = line.tenant_id,
                product_name = version.name,
                channel_code = version.channel_code,
                geography = version.geography
            FROM commercial.inventory_product_versions version
            WHERE version.tenant_id = line.tenant_id
              AND version.id = line.product_version_id;
            ALTER TABLE commercial.media_plan_lines
                ENABLE TRIGGER protect_media_plan_lines;
            ALTER TABLE commercial.media_plan_lines
                ALTER COLUMN inventory_tenant_id SET NOT NULL,
                ALTER COLUMN product_name SET NOT NULL,
                ALTER COLUMN channel_code SET NOT NULL,
                ALTER COLUMN geography SET NOT NULL,
                DROP CONSTRAINT fk_media_plan_line_product,
                DROP CONSTRAINT fk_media_plan_line_product_version,
                DROP CONSTRAINT fk_media_plan_line_rate,
                DROP CONSTRAINT fk_media_plan_line_availability,
                ADD CONSTRAINT fk_media_plan_line_product FOREIGN KEY (
                    inventory_tenant_id, inventory_product_id)
                    REFERENCES commercial.inventory_products (tenant_id, id),
                ADD CONSTRAINT fk_media_plan_line_product_version FOREIGN KEY (
                    inventory_tenant_id, product_version_id)
                    REFERENCES commercial.inventory_product_versions (tenant_id, id),
                ADD CONSTRAINT fk_media_plan_line_rate FOREIGN KEY (
                    inventory_tenant_id, rate_id)
                    REFERENCES commercial.inventory_rates (tenant_id, id),
                ADD CONSTRAINT fk_media_plan_line_availability FOREIGN KEY (
                    inventory_tenant_id, availability_id)
                    REFERENCES commercial.inventory_availability (tenant_id, id),
                ADD CONSTRAINT fk_media_plan_line_listing FOREIGN KEY (
                    inventory_tenant_id, marketplace_listing_version_id)
                    REFERENCES commercial.marketplace_listing_versions (
                        supplier_tenant_id, id),
                ADD CONSTRAINT ck_media_plan_line_supply_owner CHECK (
                    marketplace_listing_version_id IS NOT NULL
                    OR inventory_tenant_id = tenant_id);
            """);

    private static void ExtendSupplyCoordinationLineage(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.supply_coordination
                ADD COLUMN supplier_tenant_id uuid,
                ADD COLUMN marketplace_listing_version_id uuid;
            ALTER TABLE commercial.supply_coordination
                DISABLE TRIGGER protect_supply_coordination;
            UPDATE commercial.supply_coordination
            SET supplier_tenant_id = tenant_id;
            ALTER TABLE commercial.supply_coordination
                ENABLE TRIGGER protect_supply_coordination;
            ALTER TABLE commercial.supply_coordination
                ALTER COLUMN supplier_tenant_id SET NOT NULL,
                DROP CONSTRAINT fk_supply_supplier,
                ADD CONSTRAINT fk_supply_supplier FOREIGN KEY (
                    supplier_tenant_id, supplier_id)
                    REFERENCES commercial.inventory_suppliers (tenant_id, id),
                ADD CONSTRAINT fk_supply_listing FOREIGN KEY (
                    supplier_tenant_id, marketplace_listing_version_id)
                    REFERENCES commercial.marketplace_listing_versions (
                        supplier_tenant_id, id),
                ADD CONSTRAINT ck_supply_coordination_owner CHECK (
                    marketplace_listing_version_id IS NOT NULL
                    OR supplier_tenant_id = tenant_id);

            CREATE INDEX ix_shortlist_candidate_listing
                ON commercial.inventory_shortlist_candidates (
                    tenant_id, marketplace_listing_version_id)
                WHERE marketplace_listing_version_id IS NOT NULL;
            CREATE INDEX ix_media_plan_line_listing
                ON commercial.media_plan_lines (
                    tenant_id, marketplace_listing_version_id)
                WHERE marketplace_listing_version_id IS NOT NULL;
            """);
}
