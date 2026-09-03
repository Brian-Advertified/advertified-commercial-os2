using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class InventoryExtractionDurabilityHardening
{
    private static void ReplaceClaimFunction(
        MigrationBuilder migrationBuilder,
        string sql) => migrationBuilder.Sql(sql);

    private const string BackedOffClaimFunctionSql = """
        CREATE OR REPLACE FUNCTION commercial.claim_next_inventory_extraction_attempt(
            p_worker_id uuid, p_lease_seconds integer)
        RETURNS TABLE (
            tenant_id uuid, attempt_id uuid, import_id uuid,
            source_file_version bigint, source_hash text, status_code text,
            stable_submission_key text, provider_name text, provider_version text,
            external_task_id text, submitted_at_utc timestamptz,
            started_at_utc timestamptz, last_polled_at_utc timestamptz,
            polling_checkpoint text, attempt_number integer, requested_by uuid,
            command_id uuid, correlation_id uuid, claim_token uuid)
        LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, commercial
        AS $claim_extraction_attempt$
        DECLARE
            now_utc timestamptz := pg_catalog.statement_timestamp();
            new_token uuid := pg_catalog.gen_random_uuid();
        BEGIN
            IF p_worker_id IS NULL OR p_lease_seconds NOT BETWEEN 30 AND 600 THEN
                RAISE EXCEPTION 'inventory extraction worker lease is invalid'
                    USING ERRCODE = '22023';
            END IF;
            RETURN QUERY
            WITH candidate AS (
                SELECT attempt.id
                FROM commercial.inventory_extraction_attempts attempt
                WHERE attempt.status_code IN (
                        'PENDING', 'SUBMITTING', 'RUNNING', 'FAILED_RETRYABLE')
                  AND attempt.extracted_artifact_id IS NULL
                  AND (attempt.worker_lease_expires_at_utc IS NULL OR
                       attempt.worker_lease_expires_at_utc <= now_utc)
                  AND (attempt.status_code <> 'FAILED_RETRYABLE' OR
                       attempt.updated_at_utc <= now_utc - interval '5 seconds')
                  AND (attempt.status_code <> 'RUNNING' OR
                       attempt.last_polled_at_utc IS NULL OR
                       attempt.last_polled_at_utc <= now_utc - interval '1 second')
                ORDER BY attempt.created_at_utc, attempt.attempt_number, attempt.id
                FOR UPDATE SKIP LOCKED LIMIT 1
            )
            UPDATE commercial.inventory_extraction_attempts attempt
            SET worker_id = p_worker_id, worker_lease_token = new_token,
                worker_lease_expires_at_utc = now_utc
                    + pg_catalog.make_interval(secs => p_lease_seconds)
            FROM candidate
            WHERE attempt.id = candidate.id
            RETURNING attempt.tenant_id, attempt.id, attempt.import_id,
                attempt.source_file_version, attempt.source_hash::text,
                attempt.status_code::text, attempt.stable_submission_key::text,
                attempt.provider_name::text, attempt.provider_version::text,
                attempt.external_task_id::text, attempt.submitted_at_utc,
                attempt.started_at_utc, attempt.last_polled_at_utc,
                attempt.polling_checkpoint::text, attempt.attempt_number,
                attempt.requested_by, attempt.command_id, attempt.correlation_id,
                attempt.worker_lease_token;
        END;
        $claim_extraction_attempt$;
        """;

    private const string OriginalClaimFunctionSql = """
        CREATE OR REPLACE FUNCTION commercial.claim_next_inventory_extraction_attempt(
            p_worker_id uuid, p_lease_seconds integer)
        RETURNS TABLE (
            tenant_id uuid, attempt_id uuid, import_id uuid,
            source_file_version bigint, source_hash text, status_code text,
            stable_submission_key text, provider_name text, provider_version text,
            external_task_id text, submitted_at_utc timestamptz,
            started_at_utc timestamptz, last_polled_at_utc timestamptz,
            polling_checkpoint text, attempt_number integer, requested_by uuid,
            command_id uuid, correlation_id uuid, claim_token uuid)
        LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, commercial
        AS $claim_extraction_attempt$
        DECLARE
            now_utc timestamptz := pg_catalog.statement_timestamp();
            new_token uuid := pg_catalog.gen_random_uuid();
        BEGIN
            IF p_worker_id IS NULL OR p_lease_seconds NOT BETWEEN 30 AND 600 THEN
                RAISE EXCEPTION 'inventory extraction worker lease is invalid'
                    USING ERRCODE = '22023';
            END IF;
            RETURN QUERY
            WITH candidate AS (
                SELECT attempt.id FROM commercial.inventory_extraction_attempts attempt
                WHERE attempt.status_code IN (
                        'PENDING', 'SUBMITTING', 'RUNNING', 'FAILED_RETRYABLE')
                  AND attempt.extracted_artifact_id IS NULL
                  AND (attempt.worker_lease_expires_at_utc IS NULL OR
                       attempt.worker_lease_expires_at_utc <= now_utc)
                ORDER BY attempt.created_at_utc, attempt.attempt_number, attempt.id
                FOR UPDATE SKIP LOCKED LIMIT 1
            )
            UPDATE commercial.inventory_extraction_attempts attempt
            SET worker_id = p_worker_id, worker_lease_token = new_token,
                worker_lease_expires_at_utc = now_utc
                    + pg_catalog.make_interval(secs => p_lease_seconds)
            FROM candidate WHERE attempt.id = candidate.id
            RETURNING attempt.tenant_id, attempt.id, attempt.import_id,
                attempt.source_file_version, attempt.source_hash::text,
                attempt.status_code::text, attempt.stable_submission_key::text,
                attempt.provider_name::text, attempt.provider_version::text,
                attempt.external_task_id::text, attempt.submitted_at_utc,
                attempt.started_at_utc, attempt.last_polled_at_utc,
                attempt.polling_checkpoint::text, attempt.attempt_number,
                attempt.requested_by, attempt.command_id, attempt.correlation_id,
                attempt.worker_lease_token;
        END;
        $claim_extraction_attempt$;
        """;
}
