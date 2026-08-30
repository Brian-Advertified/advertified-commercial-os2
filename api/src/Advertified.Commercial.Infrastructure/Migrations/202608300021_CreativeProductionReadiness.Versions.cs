using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class CreativeProductionReadiness
{
    private static void CreateCreativeVersionAndReviewTables(
        MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
            """
            CREATE TABLE commercial.creative_asset_versions (
                id uuid NOT NULL,
                buyer_tenant_id uuid NOT NULL,
                supplier_tenant_id uuid NOT NULL,
                asset_id uuid NOT NULL,
                requirement_id uuid NOT NULL,
                version_number integer NOT NULL,
                asset_type_collection_code varchar(100) NOT NULL DEFAULT 'assetTypes',
                asset_type_code varchar(100) NOT NULL DEFAULT 'CREATIVE_FILE',
                file_name varchar(255) NOT NULL,
                media_type varchar(100) NOT NULL,
                size_bytes bigint NOT NULL,
                content_sha256 char(64) NOT NULL,
                protected_object_key varchar(1000) NOT NULL,
                approved_copy varchar(5000) NOT NULL,
                commercial_snapshot_json jsonb NOT NULL,
                campaign_version bigint NOT NULL,
                booking_version bigint NOT NULL,
                created_by uuid NOT NULL,
                created_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_creative_version_parties_id UNIQUE (
                    buyer_tenant_id, supplier_tenant_id, id),
                CONSTRAINT ux_creative_version_number UNIQUE (
                    buyer_tenant_id, asset_id, version_number),
                CONSTRAINT ck_creative_version_number CHECK (
                    version_number > 0 AND campaign_version > 0 AND booking_version > 0),
                CONSTRAINT ck_creative_version_size CHECK (
                    size_bytes > 0 AND size_bytes <= 104857600),
                CONSTRAINT ck_creative_version_hash CHECK (
                    content_sha256 ~ '^[0-9a-f]{64}$'),
                CONSTRAINT ck_creative_version_asset_type CHECK (
                    asset_type_collection_code = 'assetTypes'
                    AND asset_type_code = 'CREATIVE_FILE'),
                CONSTRAINT fk_creative_version_asset FOREIGN KEY (
                    buyer_tenant_id, supplier_tenant_id, asset_id)
                    REFERENCES commercial.creative_assets (
                        buyer_tenant_id, supplier_tenant_id, id),
                CONSTRAINT fk_creative_version_requirement FOREIGN KEY (
                    buyer_tenant_id, supplier_tenant_id, requirement_id)
                    REFERENCES commercial.creative_requirements (
                        buyer_tenant_id, supplier_tenant_id, id),
                CONSTRAINT fk_creative_version_type FOREIGN KEY (
                    asset_type_collection_code, asset_type_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_creative_version_creator FOREIGN KEY (created_by)
                    REFERENCES commercial.users (id)
            );

            ALTER TABLE commercial.creative_assets
                ADD CONSTRAINT fk_creative_asset_current_version FOREIGN KEY (
                    buyer_tenant_id, supplier_tenant_id, current_version_id)
                    REFERENCES commercial.creative_asset_versions (
                        buyer_tenant_id, supplier_tenant_id, id);

            CREATE TABLE commercial.creative_asset_reviews (
                id uuid NOT NULL,
                buyer_tenant_id uuid NOT NULL,
                supplier_tenant_id uuid NOT NULL,
                asset_id uuid NOT NULL,
                asset_version_id uuid NOT NULL,
                review_type_collection_code varchar(100) NOT NULL DEFAULT 'creativeReviewTypes',
                review_type_code varchar(100) NOT NULL,
                decision_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                decision_code varchar(100) NOT NULL,
                rights_collection_code varchar(100) DEFAULT 'assetRightsStatuses',
                rights_status_code varchar(100),
                evidence_reference varchar(500) NOT NULL,
                reason varchar(1000) NOT NULL,
                reviewed_by uuid NOT NULL,
                reviewer_tenant_id uuid NOT NULL,
                reviewed_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_creative_review_version_type UNIQUE (
                    buyer_tenant_id, asset_version_id, review_type_code),
                CONSTRAINT ck_creative_review_type_collection CHECK (
                    review_type_collection_code = 'creativeReviewTypes'),
                CONSTRAINT ck_creative_review_decision_collection CHECK (
                    decision_collection_code = 'lifecycleStatuses'
                    AND decision_code IN ('APPROVED', 'REJECTED')),
                CONSTRAINT ck_creative_review_rights_collection CHECK (
                    rights_collection_code = 'assetRightsStatuses'),
                CONSTRAINT ck_creative_review_shape CHECK (
                    (review_type_code = 'BRAND_LEGAL_RIGHTS'
                        AND reviewer_tenant_id = buyer_tenant_id
                        AND rights_status_code IS NOT NULL
                        AND (decision_code <> 'APPROVED' OR rights_status_code = 'APPROVED'))
                    OR (review_type_code = 'SUPPLIER_TECHNICAL'
                        AND reviewer_tenant_id = supplier_tenant_id
                        AND rights_status_code IS NULL)),
                CONSTRAINT fk_creative_review_asset FOREIGN KEY (
                    buyer_tenant_id, supplier_tenant_id, asset_id)
                    REFERENCES commercial.creative_assets (
                        buyer_tenant_id, supplier_tenant_id, id),
                CONSTRAINT fk_creative_review_version FOREIGN KEY (
                    buyer_tenant_id, supplier_tenant_id, asset_version_id)
                    REFERENCES commercial.creative_asset_versions (
                        buyer_tenant_id, supplier_tenant_id, id),
                CONSTRAINT fk_creative_review_type FOREIGN KEY (
                    review_type_collection_code, review_type_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_creative_review_decision FOREIGN KEY (
                    decision_collection_code, decision_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_creative_review_rights FOREIGN KEY (
                    rights_collection_code, rights_status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_creative_review_reviewer FOREIGN KEY (reviewed_by)
                    REFERENCES commercial.users (id),
                CONSTRAINT fk_creative_review_tenant FOREIGN KEY (reviewer_tenant_id)
                    REFERENCES commercial.tenants (id)
            );

            CREATE INDEX ix_creative_review_asset
                ON commercial.creative_asset_reviews (
                    buyer_tenant_id, asset_id, asset_version_id, review_type_code);
            """);
}
