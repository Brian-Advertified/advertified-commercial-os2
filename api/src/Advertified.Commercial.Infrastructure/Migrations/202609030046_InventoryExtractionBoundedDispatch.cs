using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609030046_InventoryExtractionBoundedDispatch")]
public sealed class InventoryExtractionBoundedDispatch : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(BoundedClaimFunctionSql);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(PreviousClaimFunctionSql);

    private const string BoundedClaimFunctionSql = """
        REVOKE ALL ON FUNCTION commercial.claim_next_inventory_extraction_attempt(
            uuid, integer) FROM PUBLIC, advertified_worker;
        DROP FUNCTION commercial.claim_next_inventory_extraction_attempt(uuid, integer);

        CREATE FUNCTION commercial.claim_next_inventory_extraction_attempt(
            p_worker_id uuid, p_lease_seconds integer, p_max_concurrency integer)
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
            IF p_worker_id IS NULL OR p_lease_seconds NOT BETWEEN 30 AND 600 OR
               p_max_concurrency NOT BETWEEN 1 AND 4 THEN
                RAISE EXCEPTION 'inventory extraction dispatch bounds are invalid'
                    USING ERRCODE = '22023';
            END IF;
            PERFORM pg_catalog.pg_advisory_xact_lock(
                pg_catalog.hashtextextended('advertified.inventory-extraction.claim', 0));
            RETURN QUERY
            WITH active AS (
                SELECT count(*) AS value
                FROM commercial.inventory_extraction_attempts item
                WHERE item.extracted_artifact_id IS NULL
                  AND (item.status_code IN (
                        'SUBMITTING', 'RUNNING', 'FAILED_RETRYABLE')
                       OR (item.status_code = 'PENDING'
                           AND item.worker_lease_expires_at_utc > now_utc))
            ), candidate AS (
                SELECT attempt.id
                FROM commercial.inventory_extraction_attempts attempt
                WHERE attempt.status_code IN (
                        'PENDING', 'SUBMITTING', 'RUNNING', 'FAILED_RETRYABLE')
                  AND attempt.extracted_artifact_id IS NULL
                  AND (attempt.worker_lease_expires_at_utc IS NULL OR
                       attempt.worker_lease_expires_at_utc <= now_utc)
                  AND (attempt.status_code <> 'FAILED_RETRYABLE' OR
                       attempt.updated_at_utc <= now_utc - interval '30 seconds')
                  AND (attempt.status_code <> 'RUNNING' OR
                       attempt.last_polled_at_utc IS NULL OR
                       attempt.last_polled_at_utc <= now_utc - interval '10 seconds')
                  AND (attempt.status_code <> 'PENDING' OR
                       (SELECT value FROM active) < p_max_concurrency)
                ORDER BY CASE attempt.status_code
                            WHEN 'RUNNING' THEN 0
                            WHEN 'FAILED_RETRYABLE' THEN 1
                            WHEN 'SUBMITTING' THEN 2
                            ELSE 3
                         END,
                         attempt.updated_at_utc, attempt.created_at_utc,
                         attempt.attempt_number, attempt.id
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

        ALTER FUNCTION commercial.claim_next_inventory_extraction_attempt(
            uuid, integer, integer) OWNER TO advertified_migrator;
        REVOKE ALL ON FUNCTION commercial.claim_next_inventory_extraction_attempt(
            uuid, integer, integer) FROM PUBLIC;
        GRANT EXECUTE ON FUNCTION commercial.claim_next_inventory_extraction_attempt(
            uuid, integer, integer) TO advertified_worker;
        """;

    private const string PreviousClaimFunctionSql = """
        REVOKE ALL ON FUNCTION commercial.claim_next_inventory_extraction_attempt(
            uuid, integer, integer) FROM PUBLIC, advertified_worker;
        DROP FUNCTION commercial.claim_next_inventory_extraction_attempt(
            uuid, integer, integer);

        CREATE FUNCTION commercial.claim_next_inventory_extraction_attempt(
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

        ALTER FUNCTION commercial.claim_next_inventory_extraction_attempt(
            uuid, integer) OWNER TO advertified_migrator;
        REVOKE ALL ON FUNCTION commercial.claim_next_inventory_extraction_attempt(
            uuid, integer) FROM PUBLIC;
        GRANT EXECUTE ON FUNCTION commercial.claim_next_inventory_extraction_attempt(
            uuid, integer) TO advertified_worker;
        """;
}
