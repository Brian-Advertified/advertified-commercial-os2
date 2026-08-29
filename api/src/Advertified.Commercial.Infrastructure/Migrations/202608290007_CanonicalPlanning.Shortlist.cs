using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class CanonicalPlanning
{
    private static void CreateShortlistTables(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.inventory_rates
                ADD CONSTRAINT ux_inventory_rates_tenant_id UNIQUE (tenant_id, id);
            ALTER TABLE commercial.inventory_availability
                ADD CONSTRAINT ux_inventory_availability_tenant_id UNIQUE (tenant_id, id);

            ALTER TABLE commercial.inventory_product_versions
                ADD COLUMN spatial_location geography(Point, 4326)
                GENERATED ALWAYS AS (
                    CASE
                        WHEN latitude IS NOT NULL AND longitude IS NOT NULL
                        THEN ST_SetSRID(ST_MakePoint(longitude, latitude), 4326)::geography
                        ELSE NULL
                    END) STORED;
            CREATE INDEX ix_inventory_product_versions_spatial_location
                ON commercial.inventory_product_versions USING GIST (spatial_location)
                WHERE spatial_location IS NOT NULL;

            CREATE TABLE commercial.inventory_shortlist_versions (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                brief_version_id uuid NOT NULL,
                mix_version_id uuid NOT NULL,
                version_no integer NOT NULL,
                input_hash char(64) NOT NULL,
                assumptions_json jsonb NOT NULL,
                status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                status_code varchar(100) NOT NULL,
                created_by uuid NOT NULL,
                version bigint NOT NULL,
                created_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_shortlist_versions_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_shortlist_versions_number UNIQUE (
                    tenant_id, brief_version_id, version_no),
                CONSTRAINT ck_shortlist_numbers CHECK (version_no > 0 AND version > 0),
                CONSTRAINT ck_shortlist_hash CHECK (input_hash ~ '^[0-9a-f]{64}$'),
                CONSTRAINT ck_shortlist_status_collection CHECK (
                    status_collection_code = 'lifecycleStatuses'),
                CONSTRAINT fk_shortlist_brief FOREIGN KEY (tenant_id, brief_version_id)
                    REFERENCES commercial.brief_versions (tenant_id, id),
                CONSTRAINT fk_shortlist_mix FOREIGN KEY (tenant_id, mix_version_id)
                    REFERENCES commercial.media_mix_versions (tenant_id, id),
                CONSTRAINT fk_shortlist_status FOREIGN KEY (
                    status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_shortlist_creator FOREIGN KEY (created_by)
                    REFERENCES commercial.users (id)
            );

            CREATE TABLE commercial.inventory_shortlist_candidates (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                shortlist_version_id uuid NOT NULL,
                inventory_product_id uuid NOT NULL,
                product_version_id uuid NOT NULL,
                rate_id uuid,
                availability_id uuid,
                is_eligible boolean NOT NULL,
                rejection_reason_collection_code varchar(100),
                rejection_reason_code varchar(100),
                rejection_detail varchar(1000),
                score numeric(9,4),
                rate_amount_minor bigint,
                currency_code varchar(100),
                channel_code varchar(100) NOT NULL,
                geography varchar(500) NOT NULL,
                input_hash char(64) NOT NULL,
                created_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_shortlist_candidates_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_shortlist_candidate_product UNIQUE (
                    tenant_id, shortlist_version_id, inventory_product_id),
                CONSTRAINT ck_shortlist_candidate_decision CHECK (
                    (is_eligible AND rejection_reason_code IS NULL) OR
                    (NOT is_eligible AND rejection_reason_code IS NOT NULL)),
                CONSTRAINT ck_shortlist_candidate_hash CHECK (input_hash ~ '^[0-9a-f]{64}$'),
                CONSTRAINT ck_shortlist_rejection_collection CHECK (
                    rejection_reason_code IS NULL OR
                    rejection_reason_collection_code = 'rejectionReasons'),
                CONSTRAINT fk_shortlist_candidate_version FOREIGN KEY (
                    tenant_id, shortlist_version_id)
                    REFERENCES commercial.inventory_shortlist_versions (tenant_id, id),
                CONSTRAINT fk_shortlist_candidate_product FOREIGN KEY (
                    tenant_id, inventory_product_id)
                    REFERENCES commercial.inventory_products (tenant_id, id),
                CONSTRAINT fk_shortlist_candidate_product_version FOREIGN KEY (
                    tenant_id, product_version_id)
                    REFERENCES commercial.inventory_product_versions (tenant_id, id),
                CONSTRAINT fk_shortlist_candidate_rate FOREIGN KEY (tenant_id, rate_id)
                    REFERENCES commercial.inventory_rates (tenant_id, id),
                CONSTRAINT fk_shortlist_candidate_availability FOREIGN KEY (
                    tenant_id, availability_id)
                    REFERENCES commercial.inventory_availability (tenant_id, id),
                CONSTRAINT fk_shortlist_candidate_rejection FOREIGN KEY (
                    rejection_reason_collection_code, rejection_reason_code)
                    REFERENCES governance.master_data_items (collection_code, code)
            );

            CREATE TABLE commercial.inventory_benchmark_snapshots (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                shortlist_candidate_id uuid NOT NULL,
                target_product_version_id uuid NOT NULL,
                target_rate_id uuid NOT NULL,
                policy_version varchar(100) NOT NULL,
                comparison_basis varchar(500) NOT NULL,
                geography_basis varchar(500) NOT NULL,
                cohort_product_version_ids_json jsonb NOT NULL,
                cohort_rate_ids_json jsonb NOT NULL,
                cohort_distances_json jsonb NOT NULL,
                exclusions_json jsonb NOT NULL,
                statistics_json jsonb NOT NULL,
                confidence numeric(5,4) NOT NULL,
                position_collection_code varchar(100) NOT NULL DEFAULT 'benchmarkPositions',
                position_code varchar(100) NOT NULL,
                created_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_benchmark_candidate UNIQUE (tenant_id, shortlist_candidate_id),
                CONSTRAINT ck_benchmark_confidence CHECK (confidence BETWEEN 0 AND 1),
                CONSTRAINT ck_benchmark_position_collection CHECK (
                    position_collection_code = 'benchmarkPositions'),
                CONSTRAINT fk_benchmark_candidate FOREIGN KEY (
                    tenant_id, shortlist_candidate_id)
                    REFERENCES commercial.inventory_shortlist_candidates (tenant_id, id),
                CONSTRAINT fk_benchmark_product_version FOREIGN KEY (
                    tenant_id, target_product_version_id)
                    REFERENCES commercial.inventory_product_versions (tenant_id, id),
                CONSTRAINT fk_benchmark_rate FOREIGN KEY (tenant_id, target_rate_id)
                    REFERENCES commercial.inventory_rates (tenant_id, id),
                CONSTRAINT fk_benchmark_position FOREIGN KEY (
                    position_collection_code, position_code)
                    REFERENCES governance.master_data_items (collection_code, code)
            );

            CREATE TABLE commercial.shortlist_selections (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                shortlist_candidate_id uuid NOT NULL,
                is_selected boolean NOT NULL,
                reason varchar(1000),
                selected_by uuid NOT NULL,
                selected_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_shortlist_selection_candidate UNIQUE (
                    tenant_id, shortlist_candidate_id),
                CONSTRAINT fk_shortlist_selection_candidate FOREIGN KEY (
                    tenant_id, shortlist_candidate_id)
                    REFERENCES commercial.inventory_shortlist_candidates (tenant_id, id),
                CONSTRAINT fk_shortlist_selection_actor FOREIGN KEY (selected_by)
                    REFERENCES commercial.users (id)
            );
            """);
    }
}
