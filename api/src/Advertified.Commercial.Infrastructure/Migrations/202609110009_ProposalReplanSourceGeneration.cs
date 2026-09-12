using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609110009_ProposalReplanSourceGeneration")]
public sealed class ProposalReplanSourceGeneration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE commercial.proposal_replan_revisions
            ADD COLUMN source_generation integer NOT NULL DEFAULT 1,
            ADD COLUMN claimed_generation integer,
            ADD CONSTRAINT ck_proposal_replan_source_generation CHECK (
                source_generation > 0
                AND (claimed_generation IS NULL OR claimed_generation > 0));

        DROP FUNCTION commercial.claim_next_proposal_replan(uuid, integer);
        CREATE FUNCTION commercial.claim_next_proposal_replan(
            p_worker_id uuid, p_lease_seconds integer)
        RETURNS TABLE (
            replan_id uuid, tenant_id uuid, source_proposal_version_id uuid,
            inventory_tenant_id uuid, replacement_release_id uuid,
            review_owner_user_id uuid, triggered_by uuid,
            affected_impact_ids_json text, attempt_number integer,
            source_generation integer, claim_token uuid)
        LANGUAGE plpgsql SECURITY DEFINER
        SET search_path TO 'pg_catalog', 'commercial'
        AS $$
        DECLARE v_claim uuid := gen_random_uuid();
        BEGIN
            IF p_worker_id IS NULL OR p_lease_seconds < 30 OR p_lease_seconds > 600 THEN
                RAISE EXCEPTION 'Invalid proposal replan worker claim';
            END IF;
            RETURN QUERY
            WITH candidate AS (
                SELECT item.id
                FROM commercial.proposal_replan_revisions item
                WHERE (item.status_code = 'PENDING'
                       AND (item.next_attempt_at_utc IS NULL
                            OR item.next_attempt_at_utc <= statement_timestamp()))
                   OR (item.status_code = 'RUNNING'
                       AND item.lease_expires_at_utc <= statement_timestamp())
                ORDER BY item.detected_at_utc, item.id
                FOR UPDATE SKIP LOCKED
                LIMIT 1
            ), changed AS (
                UPDATE commercial.proposal_replan_revisions item
                SET status_code = 'RUNNING',
                    attempts = item.attempts + 1,
                    lease_owner = p_worker_id,
                    lease_token = v_claim,
                    lease_expires_at_utc = statement_timestamp()
                        + make_interval(secs => p_lease_seconds),
                    claimed_generation = item.source_generation,
                    next_attempt_at_utc = NULL,
                    error_code = NULL,
                    error_detail = NULL,
                    updated_at_utc = statement_timestamp(),
                    version = item.version + 1
                FROM candidate
                WHERE item.id = candidate.id
                RETURNING item.*
            )
            SELECT changed.id, changed.tenant_id,
                changed.source_proposal_version_id,
                changed.inventory_tenant_id,
                changed.replacement_release_id,
                changed.review_owner_user_id,
                changed.triggered_by,
                changed.affected_impact_ids_json::text,
                changed.attempts,
                changed.source_generation,
                changed.lease_token
            FROM changed;
        END;
        $$;
        REVOKE ALL ON FUNCTION commercial.claim_next_proposal_replan(uuid, integer)
            FROM PUBLIC;
        GRANT EXECUTE ON FUNCTION commercial.claim_next_proposal_replan(uuid, integer)
            TO advertified_worker;

        CREATE FUNCTION commercial.requeue_proposal_replans_for_listing(
            p_inventory_tenant_id uuid,
            p_product_id uuid,
            p_actor_id uuid,
            p_updated_at_utc timestamptz)
        RETURNS integer
        LANGUAGE plpgsql SECURITY DEFINER
        SET search_path TO 'pg_catalog', 'commercial'
        AS $$
        DECLARE changed_count integer;
        DECLARE current_release uuid;
        BEGIN
            IF p_inventory_tenant_id IS NULL OR
                p_inventory_tenant_id IS DISTINCT FROM commercial.current_tenant_id() THEN
                RAISE EXCEPTION 'Inventory tenant does not match the current session';
            END IF;
            IF p_actor_id IS NULL OR
                p_actor_id IS DISTINCT FROM commercial.current_user_id() THEN
                RAISE EXCEPTION 'Actor does not match the current session';
            END IF;
            SELECT version.inventory_release_id INTO current_release
            FROM commercial.inventory_products product
            JOIN commercial.inventory_product_versions version
              ON version.tenant_id = product.tenant_id
             AND version.id = product.current_version_id
            WHERE product.tenant_id = p_inventory_tenant_id
              AND product.id = p_product_id
              AND product.status_code = 'ACTIVE';
            IF current_release IS NULL THEN RETURN 0; END IF;

            UPDATE commercial.proposal_replan_revisions replan
            SET source_generation = replan.source_generation + 1,
                status_code = CASE WHEN replan.status_code = 'RUNNING'
                    THEN replan.status_code ELSE 'PENDING' END,
                proposed_revision_json = CASE WHEN replan.status_code = 'RUNNING'
                    THEN replan.proposed_revision_json ELSE NULL END,
                comparison_json = CASE WHEN replan.status_code = 'RUNNING'
                    THEN replan.comparison_json ELSE NULL END,
                completed_at_utc = CASE WHEN replan.status_code = 'RUNNING'
                    THEN replan.completed_at_utc ELSE NULL END,
                next_attempt_at_utc = CASE WHEN replan.status_code = 'RUNNING'
                    THEN replan.next_attempt_at_utc ELSE NULL END,
                error_code = CASE WHEN replan.status_code = 'RUNNING'
                    THEN replan.error_code ELSE NULL END,
                error_detail = CASE WHEN replan.status_code = 'RUNNING'
                    THEN replan.error_detail ELSE NULL END,
                updated_at_utc = p_updated_at_utc,
                version = replan.version + 1
            WHERE replan.inventory_tenant_id = p_inventory_tenant_id
              AND replan.replacement_release_id = current_release
              AND replan.status_code IN ('PENDING', 'RUNNING', 'REVIEW_REQUIRED', 'FAILED')
              AND EXISTS (
                  SELECT 1 FROM commercial.proposal_inventory_impacts impact
                  WHERE impact.tenant_id = replan.tenant_id
                    AND impact.proposal_version_id = replan.source_proposal_version_id
                    AND impact.replacement_release_id = current_release
                    AND impact.replacement_product_id = p_product_id
                    AND impact.status_code = 'OPEN');
            GET DIAGNOSTICS changed_count = ROW_COUNT;
            RETURN changed_count;
        END;
        $$;
        REVOKE ALL ON FUNCTION commercial.requeue_proposal_replans_for_listing(
            uuid, uuid, uuid, timestamptz) FROM PUBLIC;
        GRANT EXECUTE ON FUNCTION commercial.requeue_proposal_replans_for_listing(
            uuid, uuid, uuid, timestamptz) TO advertified_app;

        DROP FUNCTION commercial.complete_proposal_replan(
            uuid, boolean, jsonb, jsonb, text, text, integer, integer);
        CREATE FUNCTION commercial.complete_proposal_replan(
            p_claim_token uuid,
            p_source_generation integer,
            p_success boolean,
            p_proposed_revision_json jsonb,
            p_comparison_json jsonb,
            p_failure_code text,
            p_failure_detail text,
            p_failure_delay_seconds integer,
            p_max_attempts integer)
        RETURNS text
        LANGUAGE plpgsql SECURITY DEFINER
        SET search_path TO 'pg_catalog', 'commercial'
        AS $$
        DECLARE row_attempts integer;
        DECLARE row_source_generation integer;
        DECLARE row_claimed_generation integer;
        BEGIN
            SELECT attempts, source_generation, claimed_generation
              INTO row_attempts, row_source_generation, row_claimed_generation
            FROM commercial.proposal_replan_revisions
            WHERE lease_token = p_claim_token
              AND status_code = 'RUNNING'
              AND lease_expires_at_utc > statement_timestamp()
            FOR UPDATE;
            IF row_attempts IS NULL THEN RETURN 'fenced'; END IF;
            IF row_source_generation <> p_source_generation OR
                row_claimed_generation <> p_source_generation THEN
                UPDATE commercial.proposal_replan_revisions
                SET status_code = 'PENDING',
                    proposed_revision_json = NULL,
                    comparison_json = NULL,
                    completed_at_utc = NULL,
                    next_attempt_at_utc = NULL,
                    lease_owner = NULL,
                    lease_token = NULL,
                    lease_expires_at_utc = NULL,
                    claimed_generation = NULL,
                    error_code = NULL,
                    error_detail = NULL,
                    updated_at_utc = statement_timestamp(),
                    version = version + 1
                WHERE lease_token = p_claim_token;
                RETURN 'superseded_by_newer_fact';
            END IF;

            IF p_success THEN
                IF p_proposed_revision_json IS NULL OR p_comparison_json IS NULL THEN
                    RAISE EXCEPTION 'Successful replan completion requires proposed revision and comparison';
                END IF;
                UPDATE commercial.proposal_replan_revisions
                SET status_code = 'REVIEW_REQUIRED',
                    proposed_revision_json = p_proposed_revision_json,
                    comparison_json = p_comparison_json,
                    completed_at_utc = statement_timestamp(),
                    lease_owner = NULL, lease_token = NULL,
                    lease_expires_at_utc = NULL, claimed_generation = NULL,
                    error_code = NULL, error_detail = NULL,
                    updated_at_utc = statement_timestamp(), version = version + 1
                WHERE lease_token = p_claim_token;
                RETURN 'completed';
            END IF;
            IF p_failure_code IS NULL OR btrim(p_failure_code) = '' THEN
                RAISE EXCEPTION 'Failed replan completion requires a failure code';
            END IF;
            IF row_attempts >= p_max_attempts THEN
                UPDATE commercial.proposal_replan_revisions
                SET status_code = 'FAILED',
                    error_code = left(p_failure_code, 100),
                    error_detail = left(coalesce(p_failure_detail, ''), 1000),
                    completed_at_utc = statement_timestamp(),
                    lease_owner = NULL, lease_token = NULL,
                    lease_expires_at_utc = NULL, claimed_generation = NULL,
                    updated_at_utc = statement_timestamp(), version = version + 1
                WHERE lease_token = p_claim_token;
                RETURN 'dead_lettered';
            END IF;
            UPDATE commercial.proposal_replan_revisions
            SET status_code = 'PENDING',
                error_code = left(p_failure_code, 100),
                error_detail = left(coalesce(p_failure_detail, ''), 1000),
                next_attempt_at_utc = statement_timestamp()
                    + make_interval(secs => p_failure_delay_seconds),
                lease_owner = NULL, lease_token = NULL,
                lease_expires_at_utc = NULL, claimed_generation = NULL,
                updated_at_utc = statement_timestamp(), version = version + 1
            WHERE lease_token = p_claim_token;
            RETURN 'retry_scheduled';
        END;
        $$;
        REVOKE ALL ON FUNCTION commercial.complete_proposal_replan(
            uuid, integer, boolean, jsonb, jsonb, text, text, integer, integer)
            FROM PUBLIC;
        GRANT EXECUTE ON FUNCTION commercial.complete_proposal_replan(
            uuid, integer, boolean, jsonb, jsonb, text, text, integer, integer)
            TO advertified_worker;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP FUNCTION IF EXISTS commercial.complete_proposal_replan(
            uuid, integer, boolean, jsonb, jsonb, text, text, integer, integer);
        DROP FUNCTION IF EXISTS commercial.requeue_proposal_replans_for_listing(
            uuid, uuid, uuid, timestamptz);
        DROP FUNCTION IF EXISTS commercial.claim_next_proposal_replan(uuid, integer);
        ALTER TABLE commercial.proposal_replan_revisions
            DROP CONSTRAINT ck_proposal_replan_source_generation,
            DROP COLUMN claimed_generation,
            DROP COLUMN source_generation;
        """);
}
