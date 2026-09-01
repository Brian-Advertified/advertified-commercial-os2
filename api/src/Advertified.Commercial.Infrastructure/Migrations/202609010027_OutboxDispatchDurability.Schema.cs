using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class OutboxDispatchDurability
{
    private const string AddDispatchStateSql =
            """
            ALTER TABLE commercial.outbox_messages
                ADD COLUMN next_attempt_at_utc timestamptz,
                ADD COLUMN claim_token uuid,
                ADD COLUMN lease_owner uuid,
                ADD COLUMN lease_expires_at_utc timestamptz,
                ADD COLUMN attempt_started_at_utc timestamptz,
                ADD COLUMN transport_reference varchar(300),
                ADD COLUMN last_failure_code varchar(100),
                ADD COLUMN last_failure_at_utc timestamptz,
                ADD COLUMN dead_lettered_at_utc timestamptz,
                ADD CONSTRAINT ck_outbox_dispatch_claim_shape CHECK (
                    (claim_token IS NULL AND lease_owner IS NULL
                        AND lease_expires_at_utc IS NULL
                        AND attempt_started_at_utc IS NULL)
                    OR (claim_token IS NOT NULL AND lease_owner IS NOT NULL
                        AND lease_expires_at_utc IS NOT NULL
                        AND attempt_started_at_utc IS NOT NULL
                        AND lease_expires_at_utc > attempt_started_at_utc
                        AND next_attempt_at_utc IS NULL
                        AND attempts > 0)),
                ADD CONSTRAINT ck_outbox_dispatch_failure_shape CHECK (
                    (last_failure_code IS NULL AND last_failure_at_utc IS NULL)
                    OR (last_failure_code IS NOT NULL
                        AND last_failure_at_utc IS NOT NULL)),
                ADD CONSTRAINT ck_outbox_dispatch_terminal_shape CHECK (
                    NOT (published_at_utc IS NOT NULL
                        AND dead_lettered_at_utc IS NOT NULL)
                    AND ((published_at_utc IS NULL
                            AND dead_lettered_at_utc IS NULL)
                        OR (claim_token IS NULL
                            AND next_attempt_at_utc IS NULL))
                    AND (dead_lettered_at_utc IS NULL
                        OR (attempts > 0
                            AND last_failure_code IS NOT NULL
                            AND last_failure_at_utc IS NOT NULL))),
                ADD CONSTRAINT ck_outbox_dispatch_transport_reference CHECK (
                    transport_reference IS NULL
                    OR (published_at_utc IS NOT NULL
                        AND btrim(transport_reference) <> '')),
                ADD CONSTRAINT ck_outbox_dispatch_failure_code CHECK (
                    last_failure_code IS NULL
                    OR last_failure_code
                        ~ '^[A-Za-z0-9][A-Za-z0-9_.:-]{0,99}$');

            DROP INDEX commercial.ix_outbox_unpublished_time;
            CREATE INDEX ix_outbox_dispatch_due
                ON commercial.outbox_messages (
                    next_attempt_at_utc, lease_expires_at_utc,
                    occurred_at_utc, id)
                WHERE published_at_utc IS NULL
                    AND dead_lettered_at_utc IS NULL;
            CREATE UNIQUE INDEX ux_outbox_dispatch_claim_token
                ON commercial.outbox_messages (claim_token)
                WHERE claim_token IS NOT NULL;

            REVOKE UPDATE, DELETE ON commercial.outbox_messages FROM advertified_app;
            """;

    private const string RemoveDispatchStateSql =
            """
            DROP INDEX commercial.ux_outbox_dispatch_claim_token;
            DROP INDEX commercial.ix_outbox_dispatch_due;
            ALTER TABLE commercial.outbox_messages
                DROP CONSTRAINT ck_outbox_dispatch_failure_code,
                DROP CONSTRAINT ck_outbox_dispatch_transport_reference,
                DROP CONSTRAINT ck_outbox_dispatch_terminal_shape,
                DROP CONSTRAINT ck_outbox_dispatch_failure_shape,
                DROP CONSTRAINT ck_outbox_dispatch_claim_shape,
                DROP COLUMN dead_lettered_at_utc,
                DROP COLUMN last_failure_at_utc,
                DROP COLUMN last_failure_code,
                DROP COLUMN transport_reference,
                DROP COLUMN attempt_started_at_utc,
                DROP COLUMN lease_expires_at_utc,
                DROP COLUMN lease_owner,
                DROP COLUMN claim_token,
                DROP COLUMN next_attempt_at_utc;
            CREATE INDEX ix_outbox_unpublished_time
                ON commercial.outbox_messages (
                    published_at_utc, occurred_at_utc, id);
            ALTER TABLE commercial.outbox_messages ENABLE ROW LEVEL SECURITY;
            ALTER TABLE commercial.outbox_messages FORCE ROW LEVEL SECURITY;
            GRANT UPDATE ON commercial.outbox_messages TO advertified_app;
            """;

    private static void AddDispatchState(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(AddDispatchStateSql);

    private static void RemoveDispatchState(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RemoveDispatchStateSql);
}
