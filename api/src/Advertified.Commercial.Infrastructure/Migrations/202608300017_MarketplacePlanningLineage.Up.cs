using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class MarketplacePlanningLineage
{
    private static void ExtendMarketplaceSnapshots(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.marketplace_listing_versions
                ADD COLUMN supplier_id uuid,
                ADD COLUMN rate_effective_from date,
                ADD COLUMN rate_effective_to date,
                ADD COLUMN availability_observed_at_utc timestamptz;
            ALTER TABLE commercial.marketplace_listing_versions
                DISABLE TRIGGER protect_marketplace_listing_versions;
            UPDATE commercial.marketplace_listing_versions snapshot
            SET supplier_id = product.supplier_id,
                rate_effective_from = rate.effective_from,
                rate_effective_to = rate.effective_to,
                availability_observed_at_utc = availability.observed_at_utc
            FROM commercial.marketplace_listings listing,
                commercial.inventory_products product,
                commercial.inventory_rates rate,
                commercial.inventory_availability availability
            WHERE listing.supplier_tenant_id = snapshot.supplier_tenant_id
              AND listing.id = snapshot.listing_id
              AND product.tenant_id = listing.supplier_tenant_id
              AND product.id = listing.product_id
              AND rate.tenant_id = snapshot.supplier_tenant_id
              AND rate.id = snapshot.rate_id
              AND availability.tenant_id = snapshot.supplier_tenant_id
              AND availability.id = snapshot.availability_id;
            ALTER TABLE commercial.marketplace_listing_versions
                ENABLE TRIGGER protect_marketplace_listing_versions;
            ALTER TABLE commercial.marketplace_listing_versions
                ALTER COLUMN supplier_id SET NOT NULL,
                ADD CONSTRAINT fk_marketplace_listing_version_supplier FOREIGN KEY (
                    supplier_tenant_id, supplier_id)
                    REFERENCES commercial.inventory_suppliers (tenant_id, id);
            """);

    private static void ExtendShortlistLineage(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.inventory_shortlist_candidates
                ADD COLUMN inventory_tenant_id uuid,
                ADD COLUMN marketplace_listing_version_id uuid,
                ADD COLUMN product_name varchar(500);
            ALTER TABLE commercial.inventory_shortlist_candidates
                DISABLE TRIGGER protect_inventory_shortlist_candidates;
            UPDATE commercial.inventory_shortlist_candidates candidate
            SET inventory_tenant_id = candidate.tenant_id,
                product_name = version.name
            FROM commercial.inventory_product_versions version
            WHERE version.tenant_id = candidate.tenant_id
              AND version.id = candidate.product_version_id;
            ALTER TABLE commercial.inventory_shortlist_candidates
                ENABLE TRIGGER protect_inventory_shortlist_candidates;
            ALTER TABLE commercial.inventory_shortlist_candidates
                ALTER COLUMN inventory_tenant_id SET NOT NULL,
                ALTER COLUMN product_name SET NOT NULL,
                DROP CONSTRAINT fk_shortlist_candidate_product,
                DROP CONSTRAINT fk_shortlist_candidate_product_version,
                DROP CONSTRAINT fk_shortlist_candidate_rate,
                DROP CONSTRAINT fk_shortlist_candidate_availability,
                ADD CONSTRAINT fk_shortlist_candidate_product FOREIGN KEY (
                    inventory_tenant_id, inventory_product_id)
                    REFERENCES commercial.inventory_products (tenant_id, id),
                ADD CONSTRAINT fk_shortlist_candidate_product_version FOREIGN KEY (
                    inventory_tenant_id, product_version_id)
                    REFERENCES commercial.inventory_product_versions (tenant_id, id),
                ADD CONSTRAINT fk_shortlist_candidate_rate FOREIGN KEY (
                    inventory_tenant_id, rate_id)
                    REFERENCES commercial.inventory_rates (tenant_id, id),
                ADD CONSTRAINT fk_shortlist_candidate_availability FOREIGN KEY (
                    inventory_tenant_id, availability_id)
                    REFERENCES commercial.inventory_availability (tenant_id, id),
                ADD CONSTRAINT fk_shortlist_candidate_listing FOREIGN KEY (
                    inventory_tenant_id, marketplace_listing_version_id)
                    REFERENCES commercial.marketplace_listing_versions (
                        supplier_tenant_id, id),
                ADD CONSTRAINT ck_shortlist_candidate_supply_owner CHECK (
                    marketplace_listing_version_id IS NOT NULL
                    OR inventory_tenant_id = tenant_id);
            """);

    private static void ExtendRecommendationAndBenchmarkLineage(
        MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
            """
            ALTER TABLE commercial.recommendation_bindings
                ADD COLUMN inventory_tenant_id uuid;
            ALTER TABLE commercial.recommendation_bindings
                DISABLE TRIGGER protect_recommendation_bindings;
            UPDATE commercial.recommendation_bindings
            SET inventory_tenant_id = tenant_id;
            ALTER TABLE commercial.recommendation_bindings
                ENABLE TRIGGER protect_recommendation_bindings;
            ALTER TABLE commercial.recommendation_bindings
                ALTER COLUMN inventory_tenant_id SET NOT NULL,
                DROP CONSTRAINT fk_recommendation_product,
                ADD CONSTRAINT fk_recommendation_product FOREIGN KEY (
                    inventory_tenant_id, inventory_product_id)
                    REFERENCES commercial.inventory_products (tenant_id, id);

            ALTER TABLE commercial.inventory_benchmark_snapshots
                ADD COLUMN inventory_tenant_id uuid;
            ALTER TABLE commercial.inventory_benchmark_snapshots
                DISABLE TRIGGER protect_inventory_benchmark_snapshots;
            UPDATE commercial.inventory_benchmark_snapshots
            SET inventory_tenant_id = tenant_id;
            ALTER TABLE commercial.inventory_benchmark_snapshots
                ENABLE TRIGGER protect_inventory_benchmark_snapshots;
            ALTER TABLE commercial.inventory_benchmark_snapshots
                ALTER COLUMN inventory_tenant_id SET NOT NULL,
                DROP CONSTRAINT fk_benchmark_product_version,
                DROP CONSTRAINT fk_benchmark_rate,
                ADD CONSTRAINT fk_benchmark_product_version FOREIGN KEY (
                    inventory_tenant_id, target_product_version_id)
                    REFERENCES commercial.inventory_product_versions (tenant_id, id),
                ADD CONSTRAINT fk_benchmark_rate FOREIGN KEY (
                    inventory_tenant_id, target_rate_id)
                    REFERENCES commercial.inventory_rates (tenant_id, id);
            """);
}
