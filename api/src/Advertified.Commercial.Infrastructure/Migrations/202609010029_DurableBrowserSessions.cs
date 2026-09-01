using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609010029_DurableBrowserSessions")]
public sealed class DurableBrowserSessions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE commercial.browser_sessions (
                token_hash varchar(64) NOT NULL,
                user_id uuid NOT NULL,
                actor_id uuid NOT NULL,
                is_service_identity boolean NOT NULL,
                created_at_utc timestamptz NOT NULL,
                expires_at_utc timestamptz NOT NULL,
                invalidated_at_utc timestamptz,
                CONSTRAINT pk_browser_sessions PRIMARY KEY (token_hash),
                CONSTRAINT fk_browser_sessions_user
                    FOREIGN KEY (user_id) REFERENCES commercial.users (id)
                    ON DELETE RESTRICT,
                CONSTRAINT ck_browser_sessions_token_hash
                    CHECK (token_hash ~ '^[0-9a-f]{64}$'),
                CONSTRAINT ck_browser_sessions_expiry
                    CHECK (expires_at_utc > created_at_utc),
                CONSTRAINT ck_browser_sessions_invalidation
                    CHECK (invalidated_at_utc IS NULL OR invalidated_at_utc >= created_at_utc)
            );

            CREATE INDEX ix_browser_sessions_expiry
                ON commercial.browser_sessions (expires_at_utc)
                WHERE invalidated_at_utc IS NULL;
            CREATE INDEX ix_browser_sessions_user_expiry
                ON commercial.browser_sessions (user_id, expires_at_utc DESC);

            REVOKE ALL ON TABLE commercial.browser_sessions FROM PUBLIC;
            REVOKE ALL ON TABLE commercial.browser_sessions FROM advertified_app;
            GRANT SELECT, INSERT, UPDATE ON TABLE commercial.browser_sessions TO advertified_app;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM commercial.browser_sessions
                    WHERE invalidated_at_utc IS NULL
                      AND expires_at_utc > CURRENT_TIMESTAMP
                ) THEN
                    RAISE EXCEPTION
                        'Cannot remove durable browser sessions while active sessions exist.';
                END IF;
            END
            $$;

            DROP TABLE commercial.browser_sessions;
            """);
    }
}
