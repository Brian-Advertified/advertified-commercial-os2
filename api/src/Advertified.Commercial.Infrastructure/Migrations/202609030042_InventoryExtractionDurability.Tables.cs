using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class InventoryExtractionDurability
{
    private static void CreateAttemptTable(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.inventory_extractions
                ADD CONSTRAINT ux_inventory_extractions_tenant_id UNIQUE (tenant_id, id);

            CREATE TABLE commercial.inventory_extraction_attempts (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                import_id uuid NOT NULL,
                source_file_version bigint NOT NULL,
                source_hash char(64) NOT NULL,
                stable_submission_key varchar(200) NOT NULL,
                provider_name varchar(100) NOT NULL,
                provider_version varchar(200) NOT NULL,
                status_collection_code varchar(100) NOT NULL
                    DEFAULT 'inventoryExtractionAttemptStatuses',
                status_code varchar(100) NOT NULL,
                external_task_id varchar(300),
                submitted_at_utc timestamptz,
                started_at_utc timestamptz,
                last_polled_at_utc timestamptz,
                completed_at_utc timestamptz,
                polling_checkpoint jsonb NOT NULL DEFAULT '{}'::jsonb,
                attempt_number integer NOT NULL,
                worker_id uuid,
                worker_lease_token uuid,
                worker_lease_expires_at_utc timestamptz,
                provider_response_code varchar(100),
                provider_error_code varchar(100),
                failure_class_collection_code varchar(100),
                failure_class_code varchar(100),
                correlation_id uuid NOT NULL,
                command_id uuid NOT NULL,
                requested_by uuid NOT NULL,
                extracted_artifact_id uuid,
                reconciliation_notes text,
                version bigint NOT NULL,
                created_at_utc timestamptz NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_inventory_extraction_attempt_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_inventory_extraction_attempt_number UNIQUE (
                    tenant_id, import_id, attempt_number),
                CONSTRAINT ux_inventory_extraction_submission_key UNIQUE (
                    tenant_id, stable_submission_key),
                CONSTRAINT ck_inventory_extraction_attempt_number CHECK (attempt_number > 0),
                CONSTRAINT ck_inventory_extraction_attempt_version CHECK (version > 0),
                CONSTRAINT ck_inventory_extraction_source_version CHECK (source_file_version > 0),
                CONSTRAINT ck_inventory_extraction_source_hash CHECK (
                    source_hash ~ '^[0-9a-f]{64}$'),
                CONSTRAINT ck_inventory_extraction_checkpoint CHECK (
                    jsonb_typeof(polling_checkpoint) = 'object'),
                CONSTRAINT ck_inventory_extraction_provider CHECK (
                    btrim(provider_name) <> '' AND btrim(provider_version) <> ''),
                CONSTRAINT ck_inventory_extraction_failure_class CHECK (
                    (failure_class_collection_code IS NULL) = (failure_class_code IS NULL)),
                CONSTRAINT ck_inventory_extraction_worker_lease CHECK (
                    (worker_id IS NULL AND worker_lease_token IS NULL AND
                     worker_lease_expires_at_utc IS NULL) OR
                    (worker_id IS NOT NULL AND worker_lease_token IS NOT NULL AND
                     worker_lease_expires_at_utc IS NOT NULL)),
                CONSTRAINT ck_inventory_extraction_completion CHECK (
                    (status_code = 'COMPLETED' AND completed_at_utc IS NOT NULL AND
                     extracted_artifact_id IS NOT NULL) OR
                    (status_code <> 'COMPLETED' AND extracted_artifact_id IS NULL)),
                CONSTRAINT ck_inventory_extraction_terminal_time CHECK (
                    status_code NOT IN (
                        'FAILED_TERMINAL', 'TIMED_OUT', 'RECONCILIATION_REQUIRED', 'CANCELLED')
                    OR completed_at_utc IS NOT NULL),
                CONSTRAINT fk_inventory_extraction_attempt_import FOREIGN KEY (
                    tenant_id, import_id)
                    REFERENCES commercial.inventory_imports (tenant_id, id),
                CONSTRAINT fk_inventory_extraction_attempt_status FOREIGN KEY (
                    status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_inventory_extraction_attempt_failure_class FOREIGN KEY (
                    failure_class_collection_code, failure_class_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_inventory_extraction_attempt_artifact FOREIGN KEY (
                    tenant_id, extracted_artifact_id)
                    REFERENCES commercial.inventory_extractions (tenant_id, id)
            );
            CREATE UNIQUE INDEX ux_inventory_extraction_external_task
                ON commercial.inventory_extraction_attempts (
                    tenant_id, provider_name, external_task_id)
                WHERE external_task_id IS NOT NULL;
            CREATE INDEX ix_inventory_extraction_attempt_claim
                ON commercial.inventory_extraction_attempts (
                    status_code, worker_lease_expires_at_utc, created_at_utc, id)
                WHERE extracted_artifact_id IS NULL;

            ALTER TABLE commercial.inventory_extractions
                ADD COLUMN attempt_id uuid,
                ADD COLUMN source_file_version bigint,
                ADD CONSTRAINT ux_inventory_extractions_attempt UNIQUE (tenant_id, attempt_id),
                ADD CONSTRAINT fk_inventory_extractions_attempt FOREIGN KEY (
                    tenant_id, attempt_id)
                    REFERENCES commercial.inventory_extraction_attempts (tenant_id, id),
                ADD CONSTRAINT ck_inventory_extractions_source_file_version CHECK (
                    source_file_version IS NULL OR source_file_version > 0);
            CREATE UNIQUE INDEX ux_inventory_extractions_source_version
                ON commercial.inventory_extractions (
                    tenant_id, import_id, source_file_version)
                WHERE source_file_version IS NOT NULL;
            """);
    }
}
