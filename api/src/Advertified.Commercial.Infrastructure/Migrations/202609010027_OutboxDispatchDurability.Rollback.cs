using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class OutboxDispatchDurability
{
    private const string GuardRollbackSql =
            """
            ALTER TABLE commercial.outbox_messages DISABLE ROW LEVEL SECURITY;

            DO $outbox_dispatch_rollback_guard$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM commercial.outbox_messages
                    WHERE next_attempt_at_utc IS NOT NULL
                       OR claim_token IS NOT NULL
                       OR lease_owner IS NOT NULL
                       OR lease_expires_at_utc IS NOT NULL
                       OR attempt_started_at_utc IS NOT NULL
                       OR transport_reference IS NOT NULL
                       OR last_failure_code IS NOT NULL
                       OR last_failure_at_utc IS NOT NULL
                       OR dead_lettered_at_utc IS NOT NULL) THEN
                    RAISE EXCEPTION
                        'outbox dispatch durability cannot roll back while dispatch evidence exists';
                END IF;
            END;
            $outbox_dispatch_rollback_guard$;
            """;

    private static void GuardRollback(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(GuardRollbackSql);
}
