using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class CanonicalCommercialFoundation
{
    private static void CreateSecurityBoundary(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE FUNCTION commercial.current_tenant_id()
            RETURNS uuid
            LANGUAGE sql
            STABLE
            PARALLEL SAFE
            AS $$
                SELECT NULLIF(
                    current_setting('advertified.tenant_id', true),
                    ''
                )::uuid
            $$;

            CREATE FUNCTION commercial.current_user_id()
            RETURNS uuid
            LANGUAGE sql
            STABLE
            PARALLEL SAFE
            AS $$
                SELECT NULLIF(
                    current_setting('advertified.user_id', true),
                    ''
                )::uuid
            $$;

            ALTER TABLE commercial.tenants ENABLE ROW LEVEL SECURITY;
            ALTER TABLE commercial.tenants FORCE ROW LEVEL SECURITY;
            ALTER TABLE commercial.users ENABLE ROW LEVEL SECURITY;
            ALTER TABLE commercial.users FORCE ROW LEVEL SECURITY;
            ALTER TABLE commercial.memberships ENABLE ROW LEVEL SECURITY;
            ALTER TABLE commercial.memberships FORCE ROW LEVEL SECURITY;
            ALTER TABLE commercial.client_accounts ENABLE ROW LEVEL SECURITY;
            ALTER TABLE commercial.client_accounts FORCE ROW LEVEL SECURITY;
            ALTER TABLE commercial.agencies ENABLE ROW LEVEL SECURITY;
            ALTER TABLE commercial.agencies FORCE ROW LEVEL SECURITY;
            ALTER TABLE commercial.contacts ENABLE ROW LEVEL SECURITY;
            ALTER TABLE commercial.contacts FORCE ROW LEVEL SECURITY;
            ALTER TABLE commercial.idempotency_records ENABLE ROW LEVEL SECURITY;
            ALTER TABLE commercial.idempotency_records FORCE ROW LEVEL SECURITY;
            ALTER TABLE commercial.audit_events ENABLE ROW LEVEL SECURITY;
            ALTER TABLE commercial.audit_events FORCE ROW LEVEL SECURITY;
            ALTER TABLE commercial.outbox_messages ENABLE ROW LEVEL SECURITY;
            ALTER TABLE commercial.outbox_messages FORCE ROW LEVEL SECURITY;

            CREATE POLICY tenant_scope ON commercial.tenants
                USING (
                    id = commercial.current_tenant_id()
                    OR EXISTS (
                        SELECT 1
                        FROM commercial.memberships membership
                        WHERE membership.tenant_id = tenants.id
                          AND membership.user_id = commercial.current_user_id()
                    )
                )
                WITH CHECK (id = commercial.current_tenant_id());

            CREATE POLICY user_scope ON commercial.users
                USING (id = commercial.current_user_id())
                WITH CHECK (id = commercial.current_user_id());

            CREATE POLICY membership_scope ON commercial.memberships
                USING (
                    tenant_id = commercial.current_tenant_id()
                    OR user_id = commercial.current_user_id()
                )
                WITH CHECK (tenant_id = commercial.current_tenant_id());

            CREATE POLICY client_account_scope ON commercial.client_accounts
                USING (tenant_id = commercial.current_tenant_id())
                WITH CHECK (tenant_id = commercial.current_tenant_id());

            CREATE POLICY agency_scope ON commercial.agencies
                USING (tenant_id = commercial.current_tenant_id())
                WITH CHECK (tenant_id = commercial.current_tenant_id());

            CREATE POLICY contact_scope ON commercial.contacts
                USING (tenant_id = commercial.current_tenant_id())
                WITH CHECK (tenant_id = commercial.current_tenant_id());

            CREATE POLICY idempotency_scope ON commercial.idempotency_records
                USING (tenant_id = commercial.current_tenant_id())
                WITH CHECK (tenant_id = commercial.current_tenant_id());

            CREATE POLICY audit_scope ON commercial.audit_events
                USING (tenant_id = commercial.current_tenant_id())
                WITH CHECK (tenant_id = commercial.current_tenant_id());

            CREATE POLICY outbox_scope ON commercial.outbox_messages
                USING (tenant_id = commercial.current_tenant_id())
                WITH CHECK (tenant_id = commercial.current_tenant_id());

            CREATE FUNCTION commercial.reject_immutable_record_change()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            BEGIN
                RAISE EXCEPTION 'Immutable commercial records cannot be changed or deleted';
            END
            $$;

            CREATE TRIGGER protect_audit_events
                BEFORE UPDATE OR DELETE ON commercial.audit_events
                FOR EACH ROW EXECUTE FUNCTION commercial.reject_immutable_record_change();

            CREATE TRIGGER protect_idempotency_records
                BEFORE UPDATE OR DELETE ON commercial.idempotency_records
                FOR EACH ROW EXECUTE FUNCTION commercial.reject_immutable_record_change();

            DO $roles$
            BEGIN
                IF (
                    SELECT count(*)
                    FROM pg_roles
                    WHERE rolname IN ('advertified_migrator', 'advertified_app')
                ) <> 2 THEN
                    RAISE EXCEPTION
                        'Advertified database roles must be provisioned before migration';
                END IF;
            END
            $roles$;

            GRANT USAGE ON SCHEMA governance, commercial TO advertified_app;
            GRANT SELECT ON governance.master_data_collections,
                governance.master_data_items TO advertified_app;
            GRANT SELECT, INSERT, UPDATE ON commercial.tenants,
                commercial.users,
                commercial.memberships,
                commercial.client_accounts,
                commercial.agencies,
                commercial.contacts TO advertified_app;
            GRANT SELECT, INSERT ON commercial.idempotency_records,
                commercial.audit_events TO advertified_app;
            GRANT SELECT, INSERT, UPDATE ON commercial.outbox_messages
                TO advertified_app;
            """);
    }
}
