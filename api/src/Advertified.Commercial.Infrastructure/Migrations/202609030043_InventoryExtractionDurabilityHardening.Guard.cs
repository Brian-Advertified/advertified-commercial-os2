using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class InventoryExtractionDurabilityHardening
{
    private static void ReplaceTransitionGuard(
        MigrationBuilder migrationBuilder,
        string sql) => migrationBuilder.Sql(sql);

    private const string HardenedTransitionGuardSql = """
        CREATE OR REPLACE FUNCTION commercial.enforce_inventory_extraction_attempt_transition()
        RETURNS trigger
        LANGUAGE plpgsql
        SET search_path = pg_catalog, commercial
        AS $attempt_transition$
        BEGIN
            IF (NEW.tenant_id, NEW.import_id, NEW.source_file_version, NEW.source_hash,
                NEW.stable_submission_key, NEW.provider_name, NEW.provider_version,
                NEW.attempt_number, NEW.correlation_id, NEW.command_id, NEW.requested_by)
               IS DISTINCT FROM
               (OLD.tenant_id, OLD.import_id, OLD.source_file_version, OLD.source_hash,
                OLD.stable_submission_key, OLD.provider_name, OLD.provider_version,
                OLD.attempt_number, OLD.correlation_id, OLD.command_id, OLD.requested_by) THEN
                RAISE EXCEPTION 'immutable extraction attempt identity cannot change'
                    USING ERRCODE = '23514';
            END IF;
            IF OLD.external_task_id IS NOT NULL AND
               NEW.external_task_id IS DISTINCT FROM OLD.external_task_id THEN
                RAISE EXCEPTION 'external extraction task identity cannot change'
                    USING ERRCODE = '23514';
            END IF;
            IF OLD.extracted_artifact_id IS NOT NULL AND
               NEW.extracted_artifact_id IS DISTINCT FROM OLD.extracted_artifact_id THEN
                RAISE EXCEPTION 'accepted extraction artifact cannot change'
                    USING ERRCODE = '23514';
            END IF;
            IF NEW.status_code <> OLD.status_code AND NOT (
                (OLD.status_code = 'PENDING' AND NEW.status_code IN (
                    'SUBMITTING', 'FAILED_TERMINAL', 'CANCELLED')) OR
                (OLD.status_code = 'SUBMITTING' AND NEW.status_code IN (
                    'RUNNING', 'FAILED_TERMINAL', 'RECONCILIATION_REQUIRED', 'CANCELLED')) OR
                (OLD.status_code = 'RUNNING' AND NEW.status_code IN (
                    'COMPLETED', 'FAILED_RETRYABLE', 'FAILED_TERMINAL', 'TIMED_OUT',
                    'RECONCILIATION_REQUIRED', 'CANCELLED')) OR
                (OLD.status_code = 'FAILED_RETRYABLE' AND NEW.status_code IN (
                    'RUNNING', 'FAILED_TERMINAL', 'TIMED_OUT',
                    'RECONCILIATION_REQUIRED', 'CANCELLED')) OR
                (OLD.status_code = 'RECONCILIATION_REQUIRED' AND
                    NEW.status_code IN ('RUNNING', 'CANCELLED'))) THEN
                RAISE EXCEPTION 'invalid extraction attempt transition: % -> %',
                    OLD.status_code, NEW.status_code USING ERRCODE = '23514';
            END IF;
            IF NEW.status_code = 'COMPLETED' AND EXISTS (
                SELECT 1 FROM commercial.inventory_extraction_attempts newer
                WHERE newer.tenant_id = NEW.tenant_id
                  AND newer.import_id = NEW.import_id
                  AND newer.attempt_number > NEW.attempt_number) THEN
                RAISE EXCEPTION 'an obsolete extraction attempt cannot be accepted'
                    USING ERRCODE = '23514';
            END IF;
            NEW.version := OLD.version + 1;
            NEW.updated_at_utc := pg_catalog.statement_timestamp();
            RETURN NEW;
        END;
        $attempt_transition$;
        """;

    private const string OriginalTransitionGuardSql = """
        CREATE OR REPLACE FUNCTION commercial.enforce_inventory_extraction_attempt_transition()
        RETURNS trigger LANGUAGE plpgsql SET search_path = pg_catalog, commercial
        AS $attempt_transition$
        BEGIN
            IF (NEW.tenant_id, NEW.import_id, NEW.source_file_version, NEW.source_hash,
                NEW.stable_submission_key, NEW.provider_name, NEW.provider_version,
                NEW.attempt_number, NEW.correlation_id, NEW.command_id, NEW.requested_by)
               IS DISTINCT FROM
               (OLD.tenant_id, OLD.import_id, OLD.source_file_version, OLD.source_hash,
                OLD.stable_submission_key, OLD.provider_name, OLD.provider_version,
                OLD.attempt_number, OLD.correlation_id, OLD.command_id, OLD.requested_by) THEN
                RAISE EXCEPTION 'immutable extraction attempt identity cannot change'
                    USING ERRCODE = '23514';
            END IF;
            IF OLD.external_task_id IS NOT NULL AND
               NEW.external_task_id IS DISTINCT FROM OLD.external_task_id THEN
                RAISE EXCEPTION 'external extraction task identity cannot change'
                    USING ERRCODE = '23514';
            END IF;
            IF OLD.extracted_artifact_id IS NOT NULL AND
               NEW.extracted_artifact_id IS DISTINCT FROM OLD.extracted_artifact_id THEN
                RAISE EXCEPTION 'accepted extraction artifact cannot change'
                    USING ERRCODE = '23514';
            END IF;
            IF NEW.status_code <> OLD.status_code AND NOT (
                (OLD.status_code = 'PENDING' AND NEW.status_code IN (
                    'SUBMITTING', 'FAILED_TERMINAL', 'CANCELLED')) OR
                (OLD.status_code = 'SUBMITTING' AND NEW.status_code IN (
                    'RUNNING', 'FAILED_TERMINAL', 'RECONCILIATION_REQUIRED', 'CANCELLED')) OR
                (OLD.status_code = 'RUNNING' AND NEW.status_code IN (
                    'COMPLETED', 'FAILED_RETRYABLE', 'FAILED_TERMINAL', 'TIMED_OUT',
                    'RECONCILIATION_REQUIRED', 'CANCELLED')) OR
                (OLD.status_code = 'FAILED_RETRYABLE' AND NEW.status_code IN (
                    'RUNNING', 'FAILED_TERMINAL', 'TIMED_OUT',
                    'RECONCILIATION_REQUIRED', 'CANCELLED')) OR
                (OLD.status_code = 'RECONCILIATION_REQUIRED' AND
                    NEW.status_code IN ('RUNNING', 'CANCELLED')) OR
                (OLD.status_code = 'TIMED_OUT' AND NEW.status_code = 'CANCELLED')) THEN
                RAISE EXCEPTION 'invalid extraction attempt transition: % -> %',
                    OLD.status_code, NEW.status_code USING ERRCODE = '23514';
            END IF;
            IF NEW.status_code = 'COMPLETED' AND EXISTS (
                SELECT 1 FROM commercial.inventory_extraction_attempts newer
                WHERE newer.tenant_id = NEW.tenant_id
                  AND newer.import_id = NEW.import_id
                  AND newer.attempt_number > NEW.attempt_number) THEN
                RAISE EXCEPTION 'an obsolete extraction attempt cannot be accepted'
                    USING ERRCODE = '23514';
            END IF;
            NEW.version := OLD.version + 1;
            NEW.updated_at_utc := pg_catalog.statement_timestamp();
            RETURN NEW;
        END;
        $attempt_transition$;
        """;
}
