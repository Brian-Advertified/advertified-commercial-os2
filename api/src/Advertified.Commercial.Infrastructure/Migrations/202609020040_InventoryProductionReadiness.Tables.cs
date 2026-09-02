using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class InventoryProductionReadiness
{
    private static void AddInventoryProductionTables(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.inventory_candidate_fields
                ADD COLUMN evidence_basis_collection_code varchar(100)
                    NOT NULL DEFAULT 'inventoryEvidenceBases',
                ADD COLUMN evidence_basis_code varchar(100)
                    NOT NULL DEFAULT 'SUPPLIER_SUPPLIED',
                ADD COLUMN verification_state_collection_code varchar(100)
                    NOT NULL DEFAULT 'inventoryEvidenceStates',
                ADD COLUMN verification_state_code varchar(100)
                    NOT NULL DEFAULT 'UNVERIFIED',
                ADD COLUMN required_action_collection_code varchar(100)
                    NOT NULL DEFAULT 'inventoryEvidenceActions',
                ADD COLUMN required_action_code varchar(100)
                    NOT NULL DEFAULT 'REVIEW',
                ADD COLUMN captured_at_utc timestamptz,
                ADD COLUMN effective_on date,
                ADD COLUMN fresh_until date,
                ADD COLUMN extraction_method_collection_code varchar(100)
                    NOT NULL DEFAULT 'inventoryExtractionMethods',
                ADD COLUMN extraction_method_code varchar(100)
                    NOT NULL DEFAULT 'TABULAR',
                ADD COLUMN extraction_confidence numeric(5,4),
                ADD CONSTRAINT ck_inventory_field_evidence_collections CHECK (
                    evidence_basis_collection_code = 'inventoryEvidenceBases'
                    AND verification_state_collection_code = 'inventoryEvidenceStates'
                    AND required_action_collection_code = 'inventoryEvidenceActions'
                    AND extraction_method_collection_code = 'inventoryExtractionMethods'),
                ADD CONSTRAINT ck_inventory_field_evidence_confidence CHECK (
                    extraction_confidence IS NULL OR extraction_confidence BETWEEN 0 AND 1),
                ADD CONSTRAINT ck_inventory_field_evidence_dates CHECK (
                    fresh_until IS NULL OR effective_on IS NULL OR fresh_until >= effective_on),
                ADD CONSTRAINT fk_inventory_field_evidence_basis FOREIGN KEY (
                    evidence_basis_collection_code, evidence_basis_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                ADD CONSTRAINT fk_inventory_field_evidence_state FOREIGN KEY (
                    verification_state_collection_code, verification_state_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                ADD CONSTRAINT fk_inventory_field_evidence_action FOREIGN KEY (
                    required_action_collection_code, required_action_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                ADD CONSTRAINT fk_inventory_field_extraction_method FOREIGN KEY (
                    extraction_method_collection_code, extraction_method_code)
                    REFERENCES governance.master_data_items (collection_code, code);
            UPDATE commercial.inventory_candidate_fields field
            SET captured_at_utc = candidate.created_at_utc
            FROM commercial.inventory_candidates candidate
            WHERE candidate.tenant_id = field.tenant_id AND candidate.id = field.candidate_id;
            ALTER TABLE commercial.inventory_candidate_fields
                ALTER COLUMN captured_at_utc SET NOT NULL;

            CREATE TABLE commercial.brief_spatial_requirements (
                id uuid NOT NULL PRIMARY KEY,
                tenant_id uuid NOT NULL,
                brief_version_id uuid NOT NULL,
                requirement_type_collection_code varchar(100) NOT NULL
                    DEFAULT 'spatialRequirementTypes',
                requirement_type_code varchar(100) NOT NULL,
                priority_collection_code varchar(100) NOT NULL
                    DEFAULT 'spatialRequirementPriorities',
                priority_code varchar(100) NOT NULL,
                label varchar(500) NOT NULL,
                raw_geometry_text text NOT NULL,
                geometry geometry(Geometry, 4326),
                radius_metres numeric(12,2),
                coverage_threshold numeric(6,5),
                buffer_inferred boolean NOT NULL DEFAULT false,
                boundary_source varchar(300),
                boundary_version varchar(100),
                source_locator varchar(1000) NOT NULL,
                is_verified boolean NOT NULL,
                created_by uuid NOT NULL,
                created_at_utc timestamptz NOT NULL,
                CONSTRAINT ux_brief_spatial_requirement_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ck_brief_spatial_collections CHECK (
                    requirement_type_collection_code = 'spatialRequirementTypes'
                    AND priority_collection_code = 'spatialRequirementPriorities'),
                CONSTRAINT ck_brief_spatial_geometry_valid CHECK (
                    NOT is_verified OR geometry IS NOT NULL AND ST_IsValid(geometry)),
                CONSTRAINT ck_brief_spatial_shape CHECK (
                    NOT is_verified OR (requirement_type_code = 'POINT_RADIUS'
                        AND ST_GeometryType(geometry) = 'ST_Point' AND radius_metres > 0)
                    OR (requirement_type_code IN ('ADMIN_BOUNDARY', 'CATCHMENT')
                        AND ST_GeometryType(geometry) IN ('ST_Polygon', 'ST_MultiPolygon')
                        AND radius_metres IS NULL)
                    OR (requirement_type_code = 'ROUTE_BUFFER'
                        AND ST_GeometryType(geometry) IN ('ST_LineString', 'ST_MultiLineString')
                        AND radius_metres > 0)),
                CONSTRAINT ck_brief_spatial_threshold CHECK (
                    coverage_threshold IS NULL OR coverage_threshold > 0
                        AND coverage_threshold <= 1),
                CONSTRAINT ck_brief_spatial_boundary_version CHECK (
                    NOT is_verified OR requirement_type_code <> 'ADMIN_BOUNDARY'
                    OR (boundary_source IS NOT NULL AND boundary_version IS NOT NULL)),
                CONSTRAINT fk_brief_spatial_version FOREIGN KEY (tenant_id, brief_version_id)
                    REFERENCES commercial.brief_versions (tenant_id, id),
                CONSTRAINT fk_brief_spatial_type FOREIGN KEY (
                    requirement_type_collection_code, requirement_type_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_brief_spatial_priority FOREIGN KEY (
                    priority_collection_code, priority_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_brief_spatial_creator FOREIGN KEY (created_by)
                    REFERENCES commercial.users (id));
            CREATE INDEX ix_brief_spatial_requirement_geometry
                ON commercial.brief_spatial_requirements USING gist (geometry);

            CREATE TABLE commercial.inventory_availability_exceptions (
                id uuid NOT NULL PRIMARY KEY,
                tenant_id uuid NOT NULL,
                product_id uuid NOT NULL,
                product_version_id uuid NOT NULL,
                exception_type_collection_code varchar(100) NOT NULL
                    DEFAULT 'availabilityExceptionTypes',
                exception_type_code varchar(100) NOT NULL,
                starts_on date NOT NULL,
                ends_on date NOT NULL,
                source_locator varchar(1000) NOT NULL,
                evidence_hash char(64) NOT NULL,
                recorded_by uuid NOT NULL,
                recorded_at_utc timestamptz NOT NULL,
                CONSTRAINT ux_inventory_availability_exception_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ck_inventory_availability_exception_collection CHECK (
                    exception_type_collection_code = 'availabilityExceptionTypes'),
                CONSTRAINT ck_inventory_availability_exception_dates CHECK (ends_on >= starts_on),
                CONSTRAINT ck_inventory_availability_exception_hash CHECK (
                    evidence_hash ~ '^[0-9a-f]{64}$'),
                CONSTRAINT fk_inventory_availability_exception_version FOREIGN KEY (
                    tenant_id, product_version_id)
                    REFERENCES commercial.inventory_product_versions (tenant_id, id),
                CONSTRAINT fk_inventory_availability_exception_product FOREIGN KEY (
                    tenant_id, product_id)
                    REFERENCES commercial.inventory_products (tenant_id, id),
                CONSTRAINT fk_inventory_availability_exception_type FOREIGN KEY (
                    exception_type_collection_code, exception_type_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_inventory_availability_exception_actor FOREIGN KEY (recorded_by)
                    REFERENCES commercial.users (id));
            CREATE INDEX ix_inventory_availability_exception_period
                ON commercial.inventory_availability_exceptions (
                    tenant_id, product_id, starts_on, ends_on);

            CREATE TABLE commercial.inventory_embedding_jobs (
                id uuid NOT NULL PRIMARY KEY,
                tenant_id uuid NOT NULL,
                product_version_id uuid NOT NULL,
                provider_code varchar(100) NOT NULL,
                model_code varchar(200) NOT NULL,
                dimensions integer NOT NULL,
                region_code varchar(30) NOT NULL,
                text_schema_version varchar(100) NOT NULL,
                normalized boolean NOT NULL,
                input_hash char(64) NOT NULL,
                provider_request_id varchar(300) NOT NULL,
                input_tokens integer NOT NULL,
                incremental_cost_usd_micros bigint NOT NULL,
                requested_by uuid NOT NULL,
                generated_at_utc timestamptz NOT NULL,
                CONSTRAINT ux_inventory_embedding_job_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ck_inventory_embedding_job_values CHECK (
                    dimensions = 1024 AND input_tokens >= 0
                    AND incremental_cost_usd_micros >= 0
                    AND normalized
                    AND input_hash ~ '^[0-9a-f]{64}$'),
                CONSTRAINT fk_inventory_embedding_job_version FOREIGN KEY (
                    tenant_id, product_version_id)
                    REFERENCES commercial.inventory_product_versions (tenant_id, id),
                CONSTRAINT fk_inventory_embedding_job_actor FOREIGN KEY (requested_by)
                    REFERENCES commercial.users (id));
            CREATE INDEX ix_inventory_embedding_job_monthly_cost
                ON commercial.inventory_embedding_jobs (tenant_id, generated_at_utc);
            ALTER TABLE commercial.inventory_product_embeddings ADD COLUMN job_id uuid;
            ALTER TABLE commercial.inventory_product_embeddings
                ADD CONSTRAINT fk_inventory_embedding_job FOREIGN KEY (tenant_id, job_id)
                REFERENCES commercial.inventory_embedding_jobs (tenant_id, id);

            ALTER TABLE commercial.inventory_asset_rights_reviews
                ADD COLUMN scope_codes jsonb NOT NULL DEFAULT '[]',
                ADD COLUMN territory_code varchar(2) NOT NULL DEFAULT 'ZA',
                ADD COLUMN effective_on date,
                ADD COLUMN until_revoked boolean NOT NULL DEFAULT false,
                ADD COLUMN attestor_role_code varchar(100),
                ADD COLUMN evidence_reference varchar(1000),
                ADD COLUMN evidence_hash char(64),
                ADD CONSTRAINT ck_inventory_asset_rights_scopes CHECK (
                    jsonb_typeof(scope_codes) = 'array'),
                ADD CONSTRAINT ck_inventory_asset_rights_territory CHECK (
                    territory_code ~ '^[A-Z]{2}$'),
                ADD CONSTRAINT ck_inventory_asset_rights_dates CHECK (
                    NOT until_revoked OR licensed_until IS NULL),
                ADD CONSTRAINT ck_inventory_asset_rights_evidence_hash CHECK (
                    evidence_hash IS NULL OR evidence_hash ~ '^[0-9a-f]{64}$');
            INSERT INTO commercial.human_tasks (
                id, tenant_id, opportunity_id, task_type_code, status_code, title,
                why_it_matters, resource_type_code, resource_id, resource_version,
                assignee_user_id, action_schema_json, version, created_at_utc)
            SELECT gen_random_uuid(), asset.tenant_id, NULL,
                'ASSET_RIGHTS_REVALIDATION', 'PENDING',
                'Revalidate inventory asset rights',
                'Legacy approval lacks governed scope, territory, dates or written evidence.',
                'inventory_asset', asset.id, rights.asset_version,
                rights.reviewed_by, '{}'::jsonb, 1, CURRENT_TIMESTAMP
            FROM commercial.inventory_assets asset
            JOIN LATERAL (
                SELECT review.* FROM commercial.inventory_asset_rights_reviews review
                WHERE review.tenant_id = asset.tenant_id AND review.asset_id = asset.id
                ORDER BY review.asset_version DESC LIMIT 1) rights ON TRUE
            WHERE rights.rights_status_code = 'APPROVED';

            ALTER TABLE commercial.inventory_shortlist_candidates
                ADD COLUMN suitability_json jsonb NOT NULL DEFAULT '{}',
                ADD COLUMN spatial_match_json jsonb NOT NULL DEFAULT '{}',
                ADD CONSTRAINT ck_shortlist_suitability_json CHECK (
                    jsonb_typeof(suitability_json) = 'object'),
                ADD CONSTRAINT ck_shortlist_spatial_match_json CHECK (
                    jsonb_typeof(spatial_match_json) = 'object');

            ALTER TABLE commercial.audience_definitions
                ADD COLUMN lsm_sem_mandatory boolean NOT NULL DEFAULT false;

            ALTER TABLE commercial.marketplace_listing_versions
                ADD COLUMN rate_source_locator varchar(1000),
                ADD COLUMN availability_source_locator varchar(1000),
                ADD COLUMN private_spatial_location geography(Point, 4326),
                ADD COLUMN private_coverage_geometry geometry(MultiPolygon, 4326),
                ADD COLUMN private_catchment_geometry geometry(MultiPolygon, 4326),
                ADD COLUMN private_route_geometry geometry(MultiLineString, 4326);
            UPDATE commercial.marketplace_listing_versions snapshot
            SET rate_source_locator = rate.source_locator,
                availability_source_locator = availability.source_locator,
                private_spatial_location = version.spatial_location,
                private_coverage_geometry = version.coverage_geometry,
                private_catchment_geometry = version.catchment_geometry,
                private_route_geometry = version.route_geometry
            FROM commercial.inventory_product_versions version,
                commercial.inventory_rates rate,
                commercial.inventory_availability availability
            WHERE version.tenant_id = snapshot.supplier_tenant_id
              AND version.id = snapshot.product_version_id
              AND rate.tenant_id = snapshot.supplier_tenant_id
              AND rate.id = snapshot.rate_id
              AND availability.tenant_id = snapshot.supplier_tenant_id
              AND availability.id = snapshot.availability_id;
            ALTER TABLE commercial.marketplace_listing_versions
                ALTER COLUMN rate_source_locator SET NOT NULL,
                ALTER COLUMN availability_source_locator SET NOT NULL;
            CREATE INDEX ix_marketplace_listing_private_location
                ON commercial.marketplace_listing_versions
                USING GIST (private_spatial_location)
                WHERE private_spatial_location IS NOT NULL;
            CREATE INDEX ix_marketplace_listing_private_coverage
                ON commercial.marketplace_listing_versions
                USING GIST (private_coverage_geometry)
                WHERE private_coverage_geometry IS NOT NULL;
            CREATE INDEX ix_marketplace_listing_private_catchment
                ON commercial.marketplace_listing_versions
                USING GIST (private_catchment_geometry)
                WHERE private_catchment_geometry IS NOT NULL;
            CREATE INDEX ix_marketplace_listing_private_route
                ON commercial.marketplace_listing_versions
                USING GIST (private_route_geometry)
                WHERE private_route_geometry IS NOT NULL;
            """);
}
