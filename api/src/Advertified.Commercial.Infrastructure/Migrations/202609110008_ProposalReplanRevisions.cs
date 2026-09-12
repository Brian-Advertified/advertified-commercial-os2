using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609110008_ProposalReplanRevisions")]
public sealed class ProposalReplanRevisions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE commercial.proposal_replan_revisions (
            id uuid PRIMARY KEY,
            tenant_id uuid NOT NULL,
            source_proposal_version_id uuid NOT NULL,
            inventory_tenant_id uuid NOT NULL,
            replacement_release_id uuid NOT NULL,
            review_owner_user_id uuid NOT NULL,
            triggered_by uuid NOT NULL,
            status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
            status_code varchar(100) NOT NULL,
            affected_impact_ids_json jsonb NOT NULL,
            proposed_revision_json jsonb,
            comparison_json jsonb,
            attempts integer NOT NULL DEFAULT 0,
            next_attempt_at_utc timestamptz,
            lease_owner uuid,
            lease_token uuid,
            lease_expires_at_utc timestamptz,
            error_code varchar(100),
            error_detail varchar(1000),
            detected_at_utc timestamptz NOT NULL,
            completed_at_utc timestamptz,
            version bigint NOT NULL,
            created_at_utc timestamptz NOT NULL,
            updated_at_utc timestamptz NOT NULL,
            CONSTRAINT uq_proposal_replan_source_release UNIQUE (
                tenant_id, source_proposal_version_id, replacement_release_id),
            CONSTRAINT fk_proposal_replan_tenant FOREIGN KEY (tenant_id)
                REFERENCES commercial.tenants(id),
            CONSTRAINT fk_proposal_replan_source FOREIGN KEY (
                tenant_id, source_proposal_version_id)
                REFERENCES commercial.proposal_versions(tenant_id, id),
            CONSTRAINT fk_proposal_replan_inventory_release FOREIGN KEY (
                inventory_tenant_id, replacement_release_id)
                REFERENCES commercial.inventory_supplier_releases(tenant_id, id),
            CONSTRAINT fk_proposal_replan_review_owner FOREIGN KEY (review_owner_user_id)
                REFERENCES commercial.users(id),
            CONSTRAINT fk_proposal_replan_trigger_actor FOREIGN KEY (triggered_by)
                REFERENCES commercial.users(id),
            CONSTRAINT fk_proposal_replan_status FOREIGN KEY (
                status_collection_code, status_code)
                REFERENCES governance.master_data_items(collection_code, code),
            CONSTRAINT ck_proposal_replan_status_collection CHECK (
                status_collection_code = 'lifecycleStatuses'),
            CONSTRAINT ck_proposal_replan_attempts CHECK (attempts >= 0),
            CONSTRAINT ck_proposal_replan_version CHECK (version > 0),
            CONSTRAINT ck_proposal_replan_impacts CHECK (
                jsonb_typeof(affected_impact_ids_json) = 'array'
                AND jsonb_array_length(affected_impact_ids_json) > 0),
            CONSTRAINT ck_proposal_replan_payloads CHECK (
                (proposed_revision_json IS NULL OR jsonb_typeof(proposed_revision_json) = 'object')
                AND (comparison_json IS NULL OR jsonb_typeof(comparison_json) = 'object')),
            CONSTRAINT ck_proposal_replan_lease CHECK (
                (lease_owner IS NULL AND lease_token IS NULL AND lease_expires_at_utc IS NULL)
                OR (lease_owner IS NOT NULL AND lease_token IS NOT NULL AND lease_expires_at_utc IS NOT NULL)),
            CONSTRAINT ck_proposal_replan_terminal CHECK (
                (status_code = 'REVIEW_REQUIRED'
                    AND proposed_revision_json IS NOT NULL
                    AND comparison_json IS NOT NULL
                    AND completed_at_utc IS NOT NULL
                    AND error_code IS NULL)
                OR (status_code = 'FAILED'
                    AND completed_at_utc IS NOT NULL
                    AND error_code IS NOT NULL)
                OR status_code IN ('PENDING', 'RUNNING'))
        );
        ALTER TABLE commercial.proposal_replan_revisions ENABLE ROW LEVEL SECURITY;
        ALTER TABLE commercial.proposal_replan_revisions FORCE ROW LEVEL SECURITY;
        CREATE POLICY proposal_replan_revisions_tenant_scope
            ON commercial.proposal_replan_revisions
            USING (tenant_id = commercial.current_tenant_id())
            WITH CHECK (tenant_id = commercial.current_tenant_id());
        CREATE INDEX ix_proposal_replan_due
            ON commercial.proposal_replan_revisions (
                status_code, next_attempt_at_utc, detected_at_utc);
        CREATE INDEX ix_proposal_replan_source
            ON commercial.proposal_replan_revisions (
                tenant_id, source_proposal_version_id, detected_at_utc DESC);

        CREATE FUNCTION commercial.register_supplier_inventory_replan_work(
            p_inventory_tenant_id uuid,
            p_supplier_id uuid,
            p_replacement_release_id uuid,
            p_actor_id uuid,
            p_detected_at_utc timestamptz)
        RETURNS integer
        LANGUAGE plpgsql SECURITY DEFINER
        SET search_path TO 'pg_catalog', 'commercial'
        AS $$
        DECLARE inserted_count integer;
        BEGIN
            IF p_inventory_tenant_id IS NULL OR
                p_inventory_tenant_id IS DISTINCT FROM commercial.current_tenant_id() THEN
                RAISE EXCEPTION 'Inventory tenant does not match the current session';
            END IF;
            IF p_actor_id IS NULL OR
                p_actor_id IS DISTINCT FROM commercial.current_user_id() THEN
                RAISE EXCEPTION 'Actor does not match the current session';
            END IF;
            IF NOT EXISTS (
                SELECT 1 FROM commercial.inventory_supplier_releases release
                WHERE release.tenant_id = p_inventory_tenant_id
                  AND release.supplier_id = p_supplier_id
                  AND release.id = p_replacement_release_id
                  AND release.status_code = 'CURRENT') THEN
                RAISE EXCEPTION 'Replacement release is not current supplier inventory';
            END IF;

            INSERT INTO commercial.proposal_replan_revisions (
                id, tenant_id, source_proposal_version_id,
                inventory_tenant_id, replacement_release_id,
                review_owner_user_id, triggered_by, status_code,
                affected_impact_ids_json, attempts, detected_at_utc,
                version, created_at_utc, updated_at_utc)
            SELECT gen_random_uuid(), impact.tenant_id, impact.proposal_version_id,
                p_inventory_tenant_id, p_replacement_release_id,
                proposal.created_by, p_actor_id, 'PENDING',
                jsonb_agg(to_jsonb(impact.id) ORDER BY impact.id),
                0, p_detected_at_utc, 1, p_detected_at_utc, p_detected_at_utc
            FROM commercial.proposal_inventory_impacts impact
            JOIN commercial.proposal_versions proposal
              ON proposal.tenant_id = impact.tenant_id
             AND proposal.id = impact.proposal_version_id
            WHERE impact.inventory_tenant_id = p_inventory_tenant_id
              AND impact.supplier_id = p_supplier_id
              AND impact.replacement_release_id = p_replacement_release_id
              AND impact.status_code = 'OPEN'
            GROUP BY impact.tenant_id, impact.proposal_version_id,
                proposal.created_by
            ON CONFLICT (tenant_id, source_proposal_version_id, replacement_release_id)
                DO UPDATE SET
                    affected_impact_ids_json = EXCLUDED.affected_impact_ids_json,
                    updated_at_utc = EXCLUDED.updated_at_utc,
                    version = commercial.proposal_replan_revisions.version + 1
                WHERE commercial.proposal_replan_revisions.status_code IN ('PENDING', 'RUNNING');
            GET DIAGNOSTICS inserted_count = ROW_COUNT;
            RETURN inserted_count;
        END;
        $$;

        CREATE FUNCTION commercial.claim_next_proposal_replan(
            p_worker_id uuid, p_lease_seconds integer)
        RETURNS TABLE (
            replan_id uuid, tenant_id uuid, source_proposal_version_id uuid,
            inventory_tenant_id uuid, replacement_release_id uuid,
            review_owner_user_id uuid, triggered_by uuid,
            affected_impact_ids_json text, attempt_number integer,
            claim_token uuid)
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
                WHERE (
                    item.status_code = 'PENDING'
                    AND (item.next_attempt_at_utc IS NULL
                         OR item.next_attempt_at_utc <= statement_timestamp()))
                   OR (
                    item.status_code = 'RUNNING'
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
                changed.lease_token
            FROM changed;
        END;
        $$;

        CREATE FUNCTION commercial.heartbeat_proposal_replan(
            p_claim_token uuid, p_lease_seconds integer)
        RETURNS boolean
        LANGUAGE plpgsql SECURITY DEFINER
        SET search_path TO 'pg_catalog', 'commercial'
        AS $$
        DECLARE changed integer;
        BEGIN
            IF p_claim_token IS NULL OR p_lease_seconds < 30 OR p_lease_seconds > 600 THEN
                RETURN false;
            END IF;
            UPDATE commercial.proposal_replan_revisions
            SET lease_expires_at_utc = statement_timestamp()
                    + make_interval(secs => p_lease_seconds),
                updated_at_utc = statement_timestamp(),
                version = version + 1
            WHERE lease_token = p_claim_token
              AND status_code = 'RUNNING'
              AND lease_expires_at_utc > statement_timestamp();
            GET DIAGNOSTICS changed = ROW_COUNT;
            RETURN changed = 1;
        END;
        $$;

        CREATE FUNCTION commercial.complete_proposal_replan(
            p_claim_token uuid,
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
        BEGIN
            SELECT attempts INTO row_attempts
            FROM commercial.proposal_replan_revisions
            WHERE lease_token = p_claim_token
              AND status_code = 'RUNNING'
              AND lease_expires_at_utc > statement_timestamp()
            FOR UPDATE;
            IF row_attempts IS NULL THEN RETURN 'fenced'; END IF;

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
                    lease_expires_at_utc = NULL,
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
                    lease_expires_at_utc = NULL,
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
                lease_expires_at_utc = NULL,
                updated_at_utc = statement_timestamp(), version = version + 1
            WHERE lease_token = p_claim_token;
            RETURN 'retry_scheduled';
        END;
        $$;

        REVOKE ALL ON FUNCTION commercial.register_supplier_inventory_replan_work(
            uuid, uuid, uuid, uuid, timestamptz) FROM PUBLIC;
        GRANT EXECUTE ON FUNCTION commercial.register_supplier_inventory_replan_work(
            uuid, uuid, uuid, uuid, timestamptz) TO advertified_app;
        REVOKE ALL ON FUNCTION commercial.claim_next_proposal_replan(uuid, integer) FROM PUBLIC;
        REVOKE ALL ON FUNCTION commercial.heartbeat_proposal_replan(uuid, integer) FROM PUBLIC;
        REVOKE ALL ON FUNCTION commercial.complete_proposal_replan(
            uuid, boolean, jsonb, jsonb, text, text, integer, integer) FROM PUBLIC;
        GRANT EXECUTE ON FUNCTION commercial.claim_next_proposal_replan(uuid, integer)
            TO advertified_worker;
        GRANT EXECUTE ON FUNCTION commercial.heartbeat_proposal_replan(uuid, integer)
            TO advertified_worker;
        GRANT EXECUTE ON FUNCTION commercial.complete_proposal_replan(
            uuid, boolean, jsonb, jsonb, text, text, integer, integer)
            TO advertified_worker;
        GRANT SELECT ON TABLE commercial.proposal_replan_revisions TO advertified_app;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP FUNCTION IF EXISTS commercial.complete_proposal_replan(
            uuid, boolean, jsonb, jsonb, text, text, integer, integer);
        DROP FUNCTION IF EXISTS commercial.heartbeat_proposal_replan(uuid, integer);
        DROP FUNCTION IF EXISTS commercial.claim_next_proposal_replan(uuid, integer);
        DROP FUNCTION IF EXISTS commercial.register_supplier_inventory_replan_work(
            uuid, uuid, uuid, uuid, timestamptz);
        DROP TABLE commercial.proposal_replan_revisions;
        """);
}
