using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class OutboxDispatchDurability
{
    private const string AddDispatchFunctionsSql =
            """
            CREATE FUNCTION commercial.protect_outbox_event_truth()
            RETURNS trigger
            LANGUAGE plpgsql
            SET search_path = pg_catalog
            AS $protect_outbox_event_truth$
            BEGIN
                IF TG_OP = 'DELETE' THEN
                    RAISE EXCEPTION 'outbox events cannot be deleted';
                END IF;
                IF NEW.id IS DISTINCT FROM OLD.id
                   OR NEW.tenant_id IS DISTINCT FROM OLD.tenant_id
                   OR NEW.causation_id IS DISTINCT FROM OLD.causation_id
                   OR NEW.correlation_id IS DISTINCT FROM OLD.correlation_id
                   OR NEW.event_type_code IS DISTINCT FROM OLD.event_type_code
                   OR NEW.aggregate_type_code IS DISTINCT FROM OLD.aggregate_type_code
                   OR NEW.aggregate_id IS DISTINCT FROM OLD.aggregate_id
                   OR NEW.aggregate_version IS DISTINCT FROM OLD.aggregate_version
                   OR NEW.payload_json IS DISTINCT FROM OLD.payload_json
                   OR NEW.occurred_at_utc IS DISTINCT FROM OLD.occurred_at_utc THEN
                    RAISE EXCEPTION 'outbox event truth is immutable';
                END IF;
                RETURN NEW;
            END;
            $protect_outbox_event_truth$;

            REVOKE ALL ON FUNCTION commercial.protect_outbox_event_truth() FROM PUBLIC;
            CREATE TRIGGER protect_outbox_event_truth
                BEFORE UPDATE OR DELETE ON commercial.outbox_messages
                FOR EACH ROW EXECUTE FUNCTION commercial.protect_outbox_event_truth();

            CREATE FUNCTION commercial.lock_next_outbox_event(
                p_tenant_id uuid, p_claimed_at_utc timestamptz)
            RETURNS TABLE (event_id uuid, attempts integer, lease_expired boolean)
            LANGUAGE sql
            SET search_path = pg_catalog
            AS $lock_next_outbox_event$
                SELECT candidate.id, candidate.attempts,
                    candidate.claim_token IS NOT NULL
                        AND candidate.lease_expires_at_utc <= p_claimed_at_utc
                FROM commercial.outbox_messages candidate
                WHERE candidate.tenant_id = p_tenant_id
                  AND candidate.published_at_utc IS NULL
                  AND candidate.dead_lettered_at_utc IS NULL
                  AND ((candidate.claim_token IS NULL
                        AND (candidate.next_attempt_at_utc IS NULL
                            OR candidate.next_attempt_at_utc <= p_claimed_at_utc))
                       OR candidate.lease_expires_at_utc <= p_claimed_at_utc)
                ORDER BY candidate.occurred_at_utc, candidate.id
                FOR UPDATE SKIP LOCKED
                LIMIT 1
            $lock_next_outbox_event$;

            CREATE FUNCTION commercial.install_outbox_claim(
                p_event_id uuid, p_tenant_id uuid, p_worker_id uuid,
                p_claimed_at_utc timestamptz, p_lease_until_utc timestamptz,
                p_lease_expired boolean)
            RETURNS void
            LANGUAGE sql
            SET search_path = pg_catalog
            AS $install_outbox_claim$
                UPDATE commercial.outbox_messages event
                SET last_failure_code = CASE WHEN p_lease_expired
                        THEN 'OUTBOX_LEASE_EXPIRED' ELSE event.last_failure_code END,
                    last_failure_at_utc = CASE WHEN p_lease_expired
                        THEN p_claimed_at_utc ELSE event.last_failure_at_utc END,
                    dead_lettered_at_utc = NULL,
                    claim_token = pg_catalog.gen_random_uuid(),
                    lease_owner = p_worker_id,
                    lease_expires_at_utc = p_lease_until_utc,
                    attempt_started_at_utc = p_claimed_at_utc,
                    next_attempt_at_utc = NULL,
                    attempts = event.attempts + 1
                WHERE event.id = p_event_id AND event.tenant_id = p_tenant_id
            $install_outbox_claim$;

            CREATE FUNCTION commercial.dead_letter_exhausted_outbox_event(
                p_event_id uuid, p_tenant_id uuid,
                p_failed_at_utc timestamptz, p_lease_expired boolean)
            RETURNS void
            LANGUAGE sql
            SET search_path = pg_catalog
            AS $dead_letter_exhausted_outbox_event$
                UPDATE commercial.outbox_messages event
                SET last_failure_code = CASE WHEN p_lease_expired
                        THEN 'OUTBOX_LEASE_EXPIRED'
                        ELSE 'OUTBOX_ATTEMPT_LIMIT_REACHED' END,
                    last_failure_at_utc = p_failed_at_utc,
                    dead_lettered_at_utc = p_failed_at_utc,
                    claim_token = NULL, lease_owner = NULL,
                    lease_expires_at_utc = NULL, attempt_started_at_utc = NULL,
                    next_attempt_at_utc = NULL
                WHERE event.id = p_event_id AND event.tenant_id = p_tenant_id
            $dead_letter_exhausted_outbox_event$;

            REVOKE ALL ON FUNCTION commercial.lock_next_outbox_event(
                uuid, timestamptz) FROM PUBLIC;
            REVOKE ALL ON FUNCTION commercial.install_outbox_claim(
                uuid, uuid, uuid, timestamptz, timestamptz, boolean) FROM PUBLIC;
            REVOKE ALL ON FUNCTION commercial.dead_letter_exhausted_outbox_event(
                uuid, uuid, timestamptz, boolean) FROM PUBLIC;

            CREATE FUNCTION commercial.claim_next_outbox_event(
                p_worker_id uuid, p_lease_seconds integer)
            RETURNS TABLE (
                event_id uuid, tenant_id uuid, causation_id uuid, correlation_id uuid,
                event_type_code text, aggregate_type_code text, aggregate_id uuid,
                aggregate_version bigint, payload_text text, occurred_at_utc timestamptz,
                claim_token uuid, attempts integer, lease_expires_at_utc timestamptz,
                dead_lettered_on_claim boolean, failure_code text)
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog
            AS $claim_next_outbox_event$
            DECLARE
                tenant_context uuid := commercial.current_tenant_id();
                claimed_at_utc timestamptz := pg_catalog.statement_timestamp();
                lease_until_utc timestamptz;
                candidate_id uuid;
                candidate_attempts integer;
                candidate_lease_expired boolean;
            BEGIN
                IF tenant_context IS NULL THEN
                    RAISE EXCEPTION 'outbox dispatch requires tenant context'
                        USING ERRCODE = '42501';
                END IF;
                IF p_worker_id IS NULL OR p_lease_seconds IS NULL
                   OR p_lease_seconds < 5 OR p_lease_seconds > 300 THEN
                    RAISE EXCEPTION 'outbox claim timing is invalid' USING ERRCODE = '22023';
                END IF;
                lease_until_utc := claimed_at_utc
                    + pg_catalog.make_interval(secs => p_lease_seconds);
                SELECT candidate.event_id, candidate.attempts, candidate.lease_expired
                INTO candidate_id, candidate_attempts, candidate_lease_expired
                FROM commercial.lock_next_outbox_event(
                    tenant_context, claimed_at_utc) candidate;
                IF candidate_id IS NULL THEN RETURN; END IF;
                IF candidate_attempts >= 4 THEN
                    PERFORM commercial.dead_letter_exhausted_outbox_event(
                        candidate_id, tenant_context, claimed_at_utc, candidate_lease_expired);
                ELSE
                    PERFORM commercial.install_outbox_claim(
                        candidate_id, tenant_context, p_worker_id, claimed_at_utc,
                        lease_until_utc, candidate_lease_expired);
                END IF;
                RETURN QUERY SELECT event.id, event.tenant_id, event.causation_id,
                    event.correlation_id, event.event_type_code::text,
                    event.aggregate_type_code::text, event.aggregate_id,
                    event.aggregate_version, event.payload_json::text, event.occurred_at_utc,
                    event.claim_token, event.attempts, event.lease_expires_at_utc,
                    candidate_attempts >= 4, event.last_failure_code::text
                FROM commercial.outbox_messages event
                WHERE event.id = candidate_id AND event.tenant_id = tenant_context;
            END;
            $claim_next_outbox_event$;

            CREATE FUNCTION commercial.heartbeat_outbox_event(
                p_event_id uuid,
                p_claim_token uuid,
                p_lease_seconds integer)
            RETURNS boolean
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog
            AS $heartbeat_outbox_event$
            DECLARE
                tenant_context uuid := commercial.current_tenant_id();
                heartbeat_at_utc timestamptz := pg_catalog.statement_timestamp();
                lease_until_utc timestamptz;
                changed_count integer;
            BEGIN
                IF tenant_context IS NULL THEN
                    RAISE EXCEPTION 'outbox dispatch requires tenant context'
                        USING ERRCODE = '42501';
                END IF;
                IF p_lease_seconds IS NULL
                   OR p_lease_seconds < 5
                   OR p_lease_seconds > 300 THEN
                    RAISE EXCEPTION 'outbox heartbeat timing is invalid'
                        USING ERRCODE = '22023';
                END IF;
                lease_until_utc := heartbeat_at_utc
                    + pg_catalog.make_interval(secs => p_lease_seconds);

                UPDATE commercial.outbox_messages event
                SET lease_expires_at_utc = lease_until_utc
                WHERE event.id = p_event_id
                  AND event.tenant_id = tenant_context
                  AND event.claim_token = p_claim_token
                  AND event.published_at_utc IS NULL
                  AND event.dead_lettered_at_utc IS NULL
                  AND event.lease_expires_at_utc > heartbeat_at_utc
                  AND lease_until_utc > event.lease_expires_at_utc;
                GET DIAGNOSTICS changed_count = ROW_COUNT;
                RETURN changed_count = 1;
            END;
            $heartbeat_outbox_event$;
            """;

    private const string RemoveDispatchFunctionsSql =
            """
            DROP FUNCTION commercial.fail_outbox_event(
                uuid, uuid, boolean, text);
            DROP FUNCTION commercial.acknowledge_outbox_event(
                uuid, uuid, text);
            DROP FUNCTION commercial.heartbeat_outbox_event(
                uuid, uuid, integer);
            DROP FUNCTION commercial.claim_next_outbox_event(
                uuid, integer);
            DROP FUNCTION commercial.dead_letter_exhausted_outbox_event(
                uuid, uuid, timestamptz, boolean);
            DROP FUNCTION commercial.install_outbox_claim(
                uuid, uuid, uuid, timestamptz, timestamptz, boolean);
            DROP FUNCTION commercial.lock_next_outbox_event(
                uuid, timestamptz);
            DROP TRIGGER protect_outbox_event_truth
                ON commercial.outbox_messages;
            DROP FUNCTION commercial.protect_outbox_event_truth();
            """;

    private static void AddDispatchFunctions(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(AddDispatchFunctionsSql);

    private static void RemoveDispatchFunctions(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RemoveDispatchFunctionsSql);
}
