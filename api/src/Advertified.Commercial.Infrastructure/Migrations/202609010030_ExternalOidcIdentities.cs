using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609010030_ExternalOidcIdentities")]
public sealed class ExternalOidcIdentities : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE commercial.external_identities (
                provider_code varchar(50) NOT NULL,
                subject_hash varchar(64) NOT NULL,
                user_id uuid NOT NULL,
                created_at_utc timestamptz NOT NULL,
                last_login_at_utc timestamptz NOT NULL,
                CONSTRAINT pk_external_identities
                    PRIMARY KEY (provider_code, subject_hash),
                CONSTRAINT ux_external_identities_provider_user
                    UNIQUE (provider_code, user_id),
                CONSTRAINT fk_external_identities_user
                    FOREIGN KEY (user_id) REFERENCES commercial.users (id)
                    ON DELETE RESTRICT,
                CONSTRAINT ck_external_identities_provider
                    CHECK (provider_code ~ '^[A-Za-z0-9][A-Za-z0-9._:-]{0,49}$'),
                CONSTRAINT ck_external_identities_subject_hash
                    CHECK (subject_hash ~ '^[0-9a-f]{64}$'),
                CONSTRAINT ck_external_identities_login_time
                    CHECK (last_login_at_utc >= created_at_utc)
            );

            CREATE INDEX ix_external_identities_user
                ON commercial.external_identities (user_id);

            REVOKE ALL ON TABLE commercial.external_identities FROM PUBLIC;
            REVOKE ALL ON TABLE commercial.external_identities FROM advertified_app;
            GRANT SELECT, INSERT, UPDATE ON TABLE commercial.external_identities TO advertified_app;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM commercial.external_identities) THEN
                    RAISE EXCEPTION
                        'Cannot remove external identity bindings while bindings exist.';
                END IF;
            END
            $$;

            DROP TABLE commercial.external_identities;
            """);
    }
}
