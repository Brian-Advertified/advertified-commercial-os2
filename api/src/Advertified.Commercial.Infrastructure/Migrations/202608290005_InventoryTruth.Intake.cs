using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class InventoryTruth
{
    private static void CreateInventoryIntakeTables(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE commercial.inventory_suppliers (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                name varchar(300) NOT NULL,
                external_reference varchar(200),
                version bigint NOT NULL DEFAULT 1,
                created_at_utc timestamptz NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_inventory_suppliers_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ck_inventory_suppliers_version CHECK (version > 0),
                CONSTRAINT fk_inventory_suppliers_tenant FOREIGN KEY (tenant_id)
                    REFERENCES commercial.tenants (id)
            );
            CREATE UNIQUE INDEX ux_inventory_suppliers_tenant_name
                ON commercial.inventory_suppliers (tenant_id, lower(name));

            CREATE TABLE commercial.inventory_imports (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                supplier_id uuid NOT NULL,
                source_file_name varchar(500) NOT NULL,
                declared_media_type varchar(200) NOT NULL,
                document_class_collection_code varchar(100),
                document_class_code varchar(100),
                status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                status_code varchar(100) NOT NULL,
                scan_status_collection_code varchar(100) NOT NULL DEFAULT 'malwareScanStatuses',
                scan_status_code varchar(100) NOT NULL,
                quarantine_object_key varchar(1000) NOT NULL,
                protected_object_key varchar(1000),
                source_hash char(64) NOT NULL,
                source_size bigint NOT NULL,
                failure_code varchar(100),
                failure_detail varchar(1000),
                created_by uuid NOT NULL,
                version bigint NOT NULL,
                created_at_utc timestamptz NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_inventory_imports_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ck_inventory_import_size CHECK (source_size > 0 AND source_size <= 104857600),
                CONSTRAINT ck_inventory_import_hash CHECK (source_hash ~ '^[0-9a-f]{64}$'),
                CONSTRAINT ck_inventory_import_version CHECK (version > 0),
                CONSTRAINT ck_inventory_import_document_collection CHECK (
                    document_class_code IS NULL OR document_class_collection_code = 'documentClasses'),
                CONSTRAINT ck_inventory_import_status_collection CHECK (
                    status_collection_code = 'lifecycleStatuses'),
                CONSTRAINT ck_inventory_import_scan_collection CHECK (
                    scan_status_collection_code = 'malwareScanStatuses'),
                CONSTRAINT fk_inventory_import_tenant FOREIGN KEY (tenant_id)
                    REFERENCES commercial.tenants (id),
                CONSTRAINT fk_inventory_import_supplier FOREIGN KEY (tenant_id, supplier_id)
                    REFERENCES commercial.inventory_suppliers (tenant_id, id),
                CONSTRAINT fk_inventory_import_document FOREIGN KEY (
                    document_class_collection_code, document_class_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_inventory_import_status FOREIGN KEY (
                    status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_inventory_import_scan_status FOREIGN KEY (
                    scan_status_collection_code, scan_status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_inventory_import_creator FOREIGN KEY (created_by)
                    REFERENCES commercial.users (id)
            );
            CREATE INDEX ix_inventory_imports_tenant_status_time
                ON commercial.inventory_imports (tenant_id, status_code, created_at_utc DESC, id);

            CREATE TABLE commercial.inventory_import_steps (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                import_id uuid NOT NULL,
                step_type_collection_code varchar(100) NOT NULL DEFAULT 'inventoryImportStepTypes',
                step_type_code varchar(100) NOT NULL,
                status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                status_code varchar(100) NOT NULL,
                outcome_json jsonb NOT NULL DEFAULT '{}',
                started_at_utc timestamptz NOT NULL,
                completed_at_utc timestamptz,
                PRIMARY KEY (id),
                CONSTRAINT ux_inventory_import_steps_step UNIQUE (tenant_id, import_id, step_type_code),
                CONSTRAINT ck_inventory_step_type_collection CHECK (
                    step_type_collection_code = 'inventoryImportStepTypes'),
                CONSTRAINT ck_inventory_step_status_collection CHECK (
                    status_collection_code = 'lifecycleStatuses'),
                CONSTRAINT fk_inventory_step_import FOREIGN KEY (tenant_id, import_id)
                    REFERENCES commercial.inventory_imports (tenant_id, id),
                CONSTRAINT fk_inventory_step_type FOREIGN KEY (
                    step_type_collection_code, step_type_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_inventory_step_status FOREIGN KEY (
                    status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code)
            );
            """);
        CreateCandidateTables(migrationBuilder);
    }

    private static void CreateCandidateTables(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE commercial.inventory_candidates (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                import_id uuid NOT NULL,
                row_number integer NOT NULL,
                status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                status_code varchar(100) NOT NULL,
                proposed_values_json jsonb NOT NULL,
                canonical_values_json jsonb NOT NULL,
                validation_json jsonb NOT NULL,
                source_locator varchar(1000) NOT NULL,
                reviewed_by uuid,
                version bigint NOT NULL,
                created_at_utc timestamptz NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_inventory_candidates_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_inventory_candidates_import_row UNIQUE (tenant_id, import_id, row_number),
                CONSTRAINT ck_inventory_candidate_numbers CHECK (row_number > 0 AND version > 0),
                CONSTRAINT ck_inventory_candidate_status_collection CHECK (
                    status_collection_code = 'lifecycleStatuses'),
                CONSTRAINT fk_inventory_candidate_import FOREIGN KEY (tenant_id, import_id)
                    REFERENCES commercial.inventory_imports (tenant_id, id),
                CONSTRAINT fk_inventory_candidate_status FOREIGN KEY (
                    status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_inventory_candidate_reviewer FOREIGN KEY (reviewed_by)
                    REFERENCES commercial.users (id)
            );
            CREATE INDEX ix_inventory_candidates_import_status
                ON commercial.inventory_candidates (tenant_id, import_id, status_code, row_number);

            CREATE TABLE commercial.inventory_candidate_fields (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                candidate_id uuid NOT NULL,
                field_name varchar(100) NOT NULL,
                raw_value text,
                normalized_value text,
                transformation_code varchar(100) NOT NULL,
                source_locator varchar(1000) NOT NULL,
                source_hash char(64) NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_inventory_candidate_fields_name UNIQUE (tenant_id, candidate_id, field_name),
                CONSTRAINT ck_inventory_candidate_field_hash CHECK (source_hash ~ '^[0-9a-f]{64}$'),
                CONSTRAINT fk_inventory_candidate_field_candidate FOREIGN KEY (tenant_id, candidate_id)
                    REFERENCES commercial.inventory_candidates (tenant_id, id)
            );

            CREATE TABLE commercial.inventory_review_decisions (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                candidate_id uuid NOT NULL,
                candidate_version bigint NOT NULL,
                decision_collection_code varchar(100) NOT NULL DEFAULT 'inventoryReviewDecisions',
                decision_code varchar(100) NOT NULL,
                rejection_reason_collection_code varchar(100),
                rejection_reason_code varchar(100),
                correction_json jsonb,
                notes varchar(2000),
                decided_by uuid NOT NULL,
                decided_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ck_inventory_review_version CHECK (candidate_version > 0),
                CONSTRAINT ck_inventory_review_decision_collection CHECK (
                    decision_collection_code = 'inventoryReviewDecisions'),
                CONSTRAINT ck_inventory_review_rejection_collection CHECK (
                    rejection_reason_code IS NULL OR rejection_reason_collection_code = 'rejectionReasons'),
                CONSTRAINT fk_inventory_review_candidate FOREIGN KEY (tenant_id, candidate_id)
                    REFERENCES commercial.inventory_candidates (tenant_id, id),
                CONSTRAINT fk_inventory_review_decision FOREIGN KEY (
                    decision_collection_code, decision_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_inventory_review_reason FOREIGN KEY (
                    rejection_reason_collection_code, rejection_reason_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_inventory_review_actor FOREIGN KEY (decided_by)
                    REFERENCES commercial.users (id)
            );
            CREATE INDEX ix_inventory_review_candidate_time
                ON commercial.inventory_review_decisions (tenant_id, candidate_id, decided_at_utc);
            """);
    }
}
