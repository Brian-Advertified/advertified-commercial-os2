using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class MarketplacePlanningLineage
{
    private static void EnsureLineageCanBeRemoved(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM commercial.inventory_shortlist_candidates
                    WHERE inventory_tenant_id <> tenant_id)
                   OR EXISTS (SELECT 1 FROM commercial.media_plan_lines
                    WHERE inventory_tenant_id <> tenant_id) THEN
                    RAISE EXCEPTION 'Marketplace planning lineage must be retained before rollback';
                END IF;
            END $$;
            DROP INDEX IF EXISTS commercial.ix_media_plan_line_listing;
            DROP INDEX IF EXISTS commercial.ix_shortlist_candidate_listing;
            """);

    private static void RestoreSupplyCoordination(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.supply_coordination
                DROP CONSTRAINT ck_supply_coordination_owner,
                DROP CONSTRAINT fk_supply_listing,
                DROP CONSTRAINT fk_supply_supplier,
                ADD CONSTRAINT fk_supply_supplier FOREIGN KEY (tenant_id, supplier_id)
                    REFERENCES commercial.inventory_suppliers (tenant_id, id),
                DROP COLUMN marketplace_listing_version_id,
                DROP COLUMN supplier_tenant_id;
            """);

    private static void RestoreMediaPlanLines(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.media_plan_lines
                DROP CONSTRAINT ck_media_plan_line_supply_owner,
                DROP CONSTRAINT fk_media_plan_line_listing,
                DROP CONSTRAINT fk_media_plan_line_product,
                DROP CONSTRAINT fk_media_plan_line_product_version,
                DROP CONSTRAINT fk_media_plan_line_rate,
                DROP CONSTRAINT fk_media_plan_line_availability,
                ADD CONSTRAINT fk_media_plan_line_product FOREIGN KEY (
                    tenant_id, inventory_product_id)
                    REFERENCES commercial.inventory_products (tenant_id, id),
                ADD CONSTRAINT fk_media_plan_line_product_version FOREIGN KEY (
                    tenant_id, product_version_id)
                    REFERENCES commercial.inventory_product_versions (tenant_id, id),
                ADD CONSTRAINT fk_media_plan_line_rate FOREIGN KEY (tenant_id, rate_id)
                    REFERENCES commercial.inventory_rates (tenant_id, id),
                ADD CONSTRAINT fk_media_plan_line_availability FOREIGN KEY (
                    tenant_id, availability_id)
                    REFERENCES commercial.inventory_availability (tenant_id, id),
                DROP COLUMN geography,
                DROP COLUMN channel_code,
                DROP COLUMN product_name,
                DROP COLUMN marketplace_listing_version_id,
                DROP COLUMN inventory_tenant_id;
            """);

    private static void RestoreRecommendationAndBenchmark(
        MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
            """
            ALTER TABLE commercial.inventory_benchmark_snapshots
                DROP CONSTRAINT fk_benchmark_product_version,
                DROP CONSTRAINT fk_benchmark_rate,
                ADD CONSTRAINT fk_benchmark_product_version FOREIGN KEY (
                    tenant_id, target_product_version_id)
                    REFERENCES commercial.inventory_product_versions (tenant_id, id),
                ADD CONSTRAINT fk_benchmark_rate FOREIGN KEY (tenant_id, target_rate_id)
                    REFERENCES commercial.inventory_rates (tenant_id, id),
                DROP COLUMN inventory_tenant_id;

            ALTER TABLE commercial.recommendation_bindings
                DROP CONSTRAINT fk_recommendation_product,
                ADD CONSTRAINT fk_recommendation_product FOREIGN KEY (
                    tenant_id, inventory_product_id)
                    REFERENCES commercial.inventory_products (tenant_id, id),
                DROP COLUMN inventory_tenant_id;
            """);

    private static void RestoreShortlistCandidates(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.inventory_shortlist_candidates
                DROP CONSTRAINT ck_shortlist_candidate_supply_owner,
                DROP CONSTRAINT fk_shortlist_candidate_listing,
                DROP CONSTRAINT fk_shortlist_candidate_product,
                DROP CONSTRAINT fk_shortlist_candidate_product_version,
                DROP CONSTRAINT fk_shortlist_candidate_rate,
                DROP CONSTRAINT fk_shortlist_candidate_availability,
                ADD CONSTRAINT fk_shortlist_candidate_product FOREIGN KEY (
                    tenant_id, inventory_product_id)
                    REFERENCES commercial.inventory_products (tenant_id, id),
                ADD CONSTRAINT fk_shortlist_candidate_product_version FOREIGN KEY (
                    tenant_id, product_version_id)
                    REFERENCES commercial.inventory_product_versions (tenant_id, id),
                ADD CONSTRAINT fk_shortlist_candidate_rate FOREIGN KEY (tenant_id, rate_id)
                    REFERENCES commercial.inventory_rates (tenant_id, id),
                ADD CONSTRAINT fk_shortlist_candidate_availability FOREIGN KEY (
                    tenant_id, availability_id)
                    REFERENCES commercial.inventory_availability (tenant_id, id),
                DROP COLUMN product_name,
                DROP COLUMN marketplace_listing_version_id,
                DROP COLUMN inventory_tenant_id;
            """);

    private static void RestoreMarketplaceSnapshots(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.marketplace_listing_versions
                DROP CONSTRAINT fk_marketplace_listing_version_supplier,
                DROP COLUMN availability_observed_at_utc,
                DROP COLUMN rate_effective_to,
                DROP COLUMN rate_effective_from,
                DROP COLUMN supplier_id;
            """);
}
