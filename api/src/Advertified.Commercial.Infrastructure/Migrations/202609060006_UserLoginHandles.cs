using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609060006_UserLoginHandles")]
public sealed class UserLoginHandles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            CREATE TABLE governance.user_login_handles (
                email_hash char(64) PRIMARY KEY,
                user_id uuid NOT NULL UNIQUE
                    REFERENCES commercial.users(id) ON DELETE CASCADE,
                CONSTRAINT ck_user_login_handle_hash
                    CHECK (email_hash ~ '^[0-9a-f]{64}$')
            );

            INSERT INTO governance.user_login_handles (email_hash, user_id)
            SELECT encode(public.digest(convert_to(lower(email), 'UTF8'), 'sha256'), 'hex'), id
            FROM commercial.users;

            CREATE FUNCTION governance.sync_user_login_handle()
            RETURNS trigger
            LANGUAGE plpgsql SECURITY DEFINER
            SET search_path TO 'pg_catalog'
            AS $$
            DECLARE
                new_hash text;
                old_hash text;
                persisted_user uuid;
            BEGIN
                new_hash := encode(public.digest(convert_to(lower(NEW.email), 'UTF8'), 'sha256'), 'hex');
                IF TG_OP = 'UPDATE' AND OLD.email IS DISTINCT FROM NEW.email THEN
                    old_hash := encode(public.digest(convert_to(lower(OLD.email), 'UTF8'), 'sha256'), 'hex');
                    DELETE FROM governance.user_login_handles
                    WHERE email_hash = old_hash AND user_id = NEW.id;
                END IF;

                INSERT INTO governance.user_login_handles (email_hash, user_id)
                VALUES (new_hash, NEW.id)
                ON CONFLICT (email_hash) DO NOTHING;

                SELECT handle.user_id INTO persisted_user
                FROM governance.user_login_handles handle
                WHERE handle.email_hash = new_hash;
                IF persisted_user IS DISTINCT FROM NEW.id THEN
                    RAISE EXCEPTION 'login email hash collision'
                        USING ERRCODE = '23505';
                END IF;
                RETURN NEW;
            END;
            $$;

            CREATE TRIGGER trg_sync_user_login_handle
            AFTER INSERT OR UPDATE OF email ON commercial.users
            FOR EACH ROW EXECUTE FUNCTION governance.sync_user_login_handle();

            REVOKE ALL ON TABLE governance.user_login_handles FROM PUBLIC;
            GRANT SELECT ON TABLE governance.user_login_handles TO advertified_app;
            REVOKE ALL ON FUNCTION governance.sync_user_login_handle() FROM PUBLIC;
            """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            DROP TRIGGER trg_sync_user_login_handle ON commercial.users;
            DROP FUNCTION governance.sync_user_login_handle();
            DROP TABLE governance.user_login_handles;
            """);
}
