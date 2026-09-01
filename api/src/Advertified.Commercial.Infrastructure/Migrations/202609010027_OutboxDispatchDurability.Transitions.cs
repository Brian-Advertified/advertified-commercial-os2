using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class OutboxDispatchDurability
{
    private const string AddDispatchTransitionsSql =
            """
            CREATE FUNCTION commercial.acknowledge_outbox_event(
                p_event_id uuid,
                p_claim_token uuid,
                p_transport_reference text)
            RETURNS boolean
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog
            AS $acknowledge_outbox_event$
            DECLARE
                tenant_context uuid := commercial.current_tenant_id();
                accepted_at_utc timestamptz := pg_catalog.statement_timestamp();
                changed_count integer;
            BEGIN
                IF tenant_context IS NULL THEN
                    RAISE EXCEPTION 'outbox dispatch requires tenant context'
                        USING ERRCODE = '42501';
                END IF;
                IF p_transport_reference IS NULL
                   OR btrim(p_transport_reference) = ''
                   OR length(p_transport_reference) > 300 THEN
                    RAISE EXCEPTION 'outbox acceptance evidence is invalid'
                        USING ERRCODE = '22023';
                END IF;

                UPDATE commercial.outbox_messages event
                SET published_at_utc = accepted_at_utc,
                    transport_reference = p_transport_reference,
                    claim_token = NULL,
                    lease_owner = NULL,
                    lease_expires_at_utc = NULL,
                    attempt_started_at_utc = NULL,
                    next_attempt_at_utc = NULL
                WHERE event.id = p_event_id
                  AND event.tenant_id = tenant_context
                  AND event.claim_token = p_claim_token
                  AND event.published_at_utc IS NULL
                  AND event.dead_lettered_at_utc IS NULL
                  AND event.lease_expires_at_utc > accepted_at_utc;
                GET DIAGNOSTICS changed_count = ROW_COUNT;
                RETURN changed_count = 1;
            END;
            $acknowledge_outbox_event$;

            CREATE FUNCTION commercial.fail_outbox_event(
                p_event_id uuid,
                p_claim_token uuid,
                p_terminal boolean,
                p_failure_code text)
            RETURNS boolean
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog
            AS $fail_outbox_event$
            DECLARE
                tenant_context uuid := commercial.current_tenant_id();
                failed_at_utc timestamptz := pg_catalog.statement_timestamp();
                changed_count integer;
            BEGIN
                IF tenant_context IS NULL THEN
                    RAISE EXCEPTION 'outbox dispatch requires tenant context'
                        USING ERRCODE = '42501';
                END IF;
                IF p_terminal IS NULL
                   OR p_failure_code IS NULL
                   OR p_failure_code !~ '^[A-Za-z0-9][A-Za-z0-9_.:-]{0,99}$' THEN
                    RAISE EXCEPTION 'outbox failure evidence is invalid'
                        USING ERRCODE = '22023';
                END IF;

                UPDATE commercial.outbox_messages event
                SET last_failure_code = p_failure_code,
                    last_failure_at_utc = failed_at_utc,
                    dead_lettered_at_utc = CASE
                        WHEN p_terminal OR event.attempts >= 4
                        THEN failed_at_utc ELSE NULL END,
                    next_attempt_at_utc = CASE
                        WHEN p_terminal OR event.attempts >= 4 THEN NULL
                        WHEN event.attempts = 1
                            THEN failed_at_utc + INTERVAL '30 seconds'
                        WHEN event.attempts = 2
                            THEN failed_at_utc + INTERVAL '2 minutes'
                        WHEN event.attempts = 3
                            THEN failed_at_utc + INTERVAL '10 minutes'
                        ELSE NULL END,
                    claim_token = NULL,
                    lease_owner = NULL,
                    lease_expires_at_utc = NULL,
                    attempt_started_at_utc = NULL
                WHERE event.id = p_event_id
                  AND event.tenant_id = tenant_context
                  AND event.claim_token = p_claim_token
                  AND event.published_at_utc IS NULL
                  AND event.dead_lettered_at_utc IS NULL
                  AND event.lease_expires_at_utc > failed_at_utc;
                GET DIAGNOSTICS changed_count = ROW_COUNT;
                RETURN changed_count = 1;
            END;
            $fail_outbox_event$;

            REVOKE ALL ON FUNCTION commercial.claim_next_outbox_event(
                uuid, integer) FROM PUBLIC;
            REVOKE ALL ON FUNCTION commercial.heartbeat_outbox_event(
                uuid, uuid, integer) FROM PUBLIC;
            REVOKE ALL ON FUNCTION commercial.acknowledge_outbox_event(
                uuid, uuid, text) FROM PUBLIC;
            REVOKE ALL ON FUNCTION commercial.fail_outbox_event(
                uuid, uuid, boolean, text) FROM PUBLIC;
            GRANT EXECUTE ON FUNCTION commercial.claim_next_outbox_event(
                uuid, integer) TO advertified_app;
            GRANT EXECUTE ON FUNCTION commercial.heartbeat_outbox_event(
                uuid, uuid, integer) TO advertified_app;
            GRANT EXECUTE ON FUNCTION commercial.acknowledge_outbox_event(
                uuid, uuid, text) TO advertified_app;
            GRANT EXECUTE ON FUNCTION commercial.fail_outbox_event(
                uuid, uuid, boolean, text) TO advertified_app;
            """;

    private static void AddDispatchTransitions(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(AddDispatchTransitionsSql);
}
