using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609010033_WorkerScheduling")]
public sealed class WorkerScheduling : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE commercial.email_worker_claims (
                inbound_email_id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                claim_token uuid NOT NULL,
                worker_id uuid NOT NULL,
                lease_expires_at_utc timestamptz NOT NULL,
                next_attempt_at_utc timestamptz,
                last_failure_code varchar(100),
                dead_lettered_at_utc timestamptz,
                attempts integer NOT NULL,
                PRIMARY KEY (inbound_email_id),
                CONSTRAINT ux_email_worker_claim_token UNIQUE (claim_token),
                CONSTRAINT ck_email_worker_attempts CHECK (attempts > 0),
                CONSTRAINT ck_email_worker_failure CHECK (
                    last_failure_code IS NULL OR
                    last_failure_code ~ '^[A-Za-z0-9][A-Za-z0-9_.:-]{0,99}$'),
                CONSTRAINT ck_email_worker_dead_letter CHECK (
                    dead_lettered_at_utc IS NULL OR
                    (last_failure_code IS NOT NULL AND next_attempt_at_utc IS NULL)),
                CONSTRAINT fk_email_worker_email FOREIGN KEY (tenant_id, inbound_email_id)
                    REFERENCES commercial.inbound_campaign_emails (tenant_id, id)
                    ON DELETE CASCADE
            );
            CREATE INDEX ix_email_worker_due
                ON commercial.email_worker_claims (
                    next_attempt_at_utc, lease_expires_at_utc, inbound_email_id)
                WHERE dead_lettered_at_utc IS NULL;
            REVOKE ALL ON TABLE commercial.email_worker_claims FROM PUBLIC;
            REVOKE ALL ON TABLE commercial.email_worker_claims FROM advertified_app;
            REVOKE ALL ON TABLE commercial.email_worker_claims FROM advertified_worker;

            CREATE FUNCTION commercial.next_outbox_tenant()
            RETURNS uuid
            LANGUAGE sql
            SECURITY DEFINER
            SET search_path = pg_catalog
            AS $next_outbox_tenant$
                SELECT event.tenant_id
                FROM commercial.outbox_messages event
                WHERE event.published_at_utc IS NULL
                  AND event.dead_lettered_at_utc IS NULL
                  AND (
                    (event.claim_token IS NULL AND
                        (event.next_attempt_at_utc IS NULL OR
                         event.next_attempt_at_utc <= pg_catalog.statement_timestamp()))
                    OR event.lease_expires_at_utc <= pg_catalog.statement_timestamp())
                ORDER BY event.occurred_at_utc, event.id
                LIMIT 1
            $next_outbox_tenant$;

            CREATE FUNCTION commercial.claim_next_email_work(
                p_worker_id uuid, p_lease_seconds integer)
            RETURNS TABLE (
                tenant_id uuid, inbound_email_id uuid, owner_user_id uuid,
                correlation_id uuid, claim_token uuid)
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog
            AS $claim_next_email_work$
            DECLARE
                now_utc timestamptz := pg_catalog.statement_timestamp();
                candidate_tenant uuid;
                candidate_email uuid;
                candidate_owner uuid;
                new_token uuid := pg_catalog.gen_random_uuid();
            BEGIN
                IF p_worker_id IS NULL OR p_lease_seconds NOT BETWEEN 30 AND 600 THEN
                    RAISE EXCEPTION 'email worker lease is invalid' USING ERRCODE = '22023';
                END IF;
                SELECT email.tenant_id, email.id, mailbox.owner_user_id
                INTO candidate_tenant, candidate_email, candidate_owner
                FROM commercial.inbound_campaign_emails email
                JOIN commercial.inbound_mailboxes mailbox
                  ON mailbox.tenant_id = email.tenant_id AND mailbox.id = email.mailbox_id
                LEFT JOIN commercial.email_proposal_automation_runs run
                  ON run.tenant_id = email.tenant_id AND run.inbound_email_id = email.id
                LEFT JOIN commercial.email_worker_claims claim
                  ON claim.inbound_email_id = email.id
                WHERE mailbox.is_enabled = TRUE
                  AND mailbox.auto_send_enabled = TRUE
                  AND (run.id IS NULL OR run.status_code = 'PROCESSING')
                  AND (claim.inbound_email_id IS NULL OR
                       (claim.dead_lettered_at_utc IS NULL AND
                        claim.lease_expires_at_utc <= now_utc AND
                        (claim.next_attempt_at_utc IS NULL OR
                         claim.next_attempt_at_utc <= now_utc)))
                ORDER BY email.received_at_utc, email.id
                FOR UPDATE OF email SKIP LOCKED
                LIMIT 1;
                IF candidate_email IS NULL THEN RETURN; END IF;

                INSERT INTO commercial.email_worker_claims (
                    inbound_email_id, tenant_id, claim_token, worker_id,
                    lease_expires_at_utc, next_attempt_at_utc,
                    last_failure_code, dead_lettered_at_utc, attempts)
                VALUES (
                    candidate_email, candidate_tenant, new_token, p_worker_id,
                    now_utc + pg_catalog.make_interval(secs => p_lease_seconds),
                    NULL, NULL, NULL, 1)
                ON CONFLICT (inbound_email_id) DO UPDATE
                SET claim_token = new_token,
                    worker_id = p_worker_id,
                    lease_expires_at_utc = now_utc
                        + pg_catalog.make_interval(secs => p_lease_seconds),
                    next_attempt_at_utc = NULL,
                    last_failure_code = NULL,
                    attempts = commercial.email_worker_claims.attempts + 1
                WHERE commercial.email_worker_claims.dead_lettered_at_utc IS NULL
                  AND commercial.email_worker_claims.lease_expires_at_utc <= now_utc
                  AND (commercial.email_worker_claims.next_attempt_at_utc IS NULL OR
                       commercial.email_worker_claims.next_attempt_at_utc <= now_utc);
                IF NOT FOUND THEN RETURN; END IF;

                RETURN QUERY SELECT candidate_tenant, candidate_email, candidate_owner,
                    pg_catalog.gen_random_uuid(), new_token;
            END;
            $claim_next_email_work$;

            CREATE FUNCTION commercial.heartbeat_email_work(
                p_claim_token uuid, p_lease_seconds integer)
            RETURNS boolean
            LANGUAGE sql
            SECURITY DEFINER
            SET search_path = pg_catalog
            AS $heartbeat_email_work$
                UPDATE commercial.email_worker_claims claim
                SET lease_expires_at_utc = pg_catalog.statement_timestamp()
                    + pg_catalog.make_interval(secs => p_lease_seconds)
                WHERE claim.claim_token = p_claim_token
                  AND claim.dead_lettered_at_utc IS NULL
                  AND claim.lease_expires_at_utc > pg_catalog.statement_timestamp()
                  AND p_lease_seconds BETWEEN 30 AND 600
                RETURNING TRUE
            $heartbeat_email_work$;

            CREATE FUNCTION commercial.complete_email_work(
                p_claim_token uuid, p_success boolean,
                p_failure_code text, p_failure_delay_seconds integer,
                p_max_attempts integer)
            RETURNS text
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog
            AS $complete_email_work$
            DECLARE
                changed integer;
                current_attempts integer;
                now_utc timestamptz := pg_catalog.statement_timestamp();
            BEGIN
                IF p_max_attempts NOT BETWEEN 1 AND 20 THEN
                    RAISE EXCEPTION 'email worker attempt limit is invalid' USING ERRCODE = '22023';
                END IF;
                IF p_success THEN
                    DELETE FROM commercial.email_worker_claims
                    WHERE claim_token = p_claim_token
                      AND dead_lettered_at_utc IS NULL
                      AND lease_expires_at_utc > now_utc;
                    GET DIAGNOSTICS changed = ROW_COUNT;
                    RETURN CASE WHEN changed = 1 THEN 'completed' ELSE 'fenced' END;
                END IF;
                IF p_failure_code IS NULL OR
                   p_failure_code !~ '^[A-Za-z0-9][A-Za-z0-9_.:-]{0,99}$' OR
                   p_failure_delay_seconds NOT BETWEEN 15 AND 900 THEN
                    RAISE EXCEPTION 'email worker failure is invalid' USING ERRCODE = '22023';
                END IF;

                SELECT attempts INTO current_attempts
                FROM commercial.email_worker_claims
                WHERE claim_token = p_claim_token
                  AND dead_lettered_at_utc IS NULL
                  AND lease_expires_at_utc > now_utc
                FOR UPDATE;
                IF current_attempts IS NULL THEN
                    RETURN 'fenced';
                END IF;
                IF current_attempts >= p_max_attempts THEN
                    UPDATE commercial.email_worker_claims
                    SET lease_expires_at_utc = now_utc,
                        next_attempt_at_utc = NULL,
                        last_failure_code = p_failure_code,
                        dead_lettered_at_utc = now_utc
                    WHERE claim_token = p_claim_token;
                    RETURN 'dead_lettered';
                END IF;
                UPDATE commercial.email_worker_claims
                SET lease_expires_at_utc = now_utc,
                    next_attempt_at_utc = now_utc
                        + pg_catalog.make_interval(secs => p_failure_delay_seconds),
                    last_failure_code = p_failure_code
                WHERE claim_token = p_claim_token;
                RETURN 'retry_scheduled';
            END;
            $complete_email_work$;

            REVOKE ALL ON FUNCTION commercial.next_outbox_tenant() FROM PUBLIC;
            REVOKE ALL ON FUNCTION commercial.claim_next_email_work(uuid, integer) FROM PUBLIC;
            REVOKE ALL ON FUNCTION commercial.heartbeat_email_work(uuid, integer) FROM PUBLIC;
            REVOKE ALL ON FUNCTION commercial.complete_email_work(
                uuid, boolean, text, integer, integer) FROM PUBLIC;
            GRANT USAGE ON SCHEMA commercial TO advertified_worker;
            GRANT EXECUTE ON FUNCTION commercial.next_outbox_tenant() TO advertified_worker;
            GRANT EXECUTE ON FUNCTION commercial.claim_next_email_work(
                uuid, integer) TO advertified_worker;
            GRANT EXECUTE ON FUNCTION commercial.heartbeat_email_work(
                uuid, integer) TO advertified_worker;
            GRANT EXECUTE ON FUNCTION commercial.complete_email_work(
                uuid, boolean, text, integer, integer) TO advertified_worker;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP FUNCTION commercial.complete_email_work(
                uuid, boolean, text, integer, integer);
            DROP FUNCTION commercial.heartbeat_email_work(uuid, integer);
            DROP FUNCTION commercial.claim_next_email_work(uuid, integer);
            DROP FUNCTION commercial.next_outbox_tenant();
            DROP TABLE commercial.email_worker_claims;
            """);
    }
}
