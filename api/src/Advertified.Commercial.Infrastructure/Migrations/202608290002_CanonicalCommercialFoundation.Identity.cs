using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class CanonicalCommercialFoundation
{
    private static void CreateIdentityTables(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE commercial.tenants (
                id uuid PRIMARY KEY,
                type_collection_code varchar(100) NOT NULL DEFAULT 'tenantTypes',
                type_code varchar(100) NOT NULL,
                legal_name varchar(200) NOT NULL,
                trading_name varchar(200) NOT NULL,
                slug varchar(100) NOT NULL,
                status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                status_code varchar(100) NOT NULL,
                timezone varchar(100) NOT NULL,
                currency_collection_code varchar(100) NOT NULL DEFAULT 'currencies',
                currency_code varchar(100) NOT NULL,
                vat_status_collection_code varchar(100) NOT NULL DEFAULT 'vatStatuses',
                vat_status_code varchar(100) NOT NULL,
                vat_number varchar(50),
                settings_json jsonb NOT NULL,
                version bigint NOT NULL,
                created_at_utc timestamptz NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                CONSTRAINT ux_tenants_slug UNIQUE (slug),
                CONSTRAINT ck_tenants_version CHECK (version > 0),
                CONSTRAINT ck_tenants_type_collection
                    CHECK (type_collection_code = 'tenantTypes'),
                CONSTRAINT ck_tenants_status_collection
                    CHECK (status_collection_code = 'lifecycleStatuses'),
                CONSTRAINT ck_tenants_currency_collection
                    CHECK (currency_collection_code = 'currencies'),
                CONSTRAINT ck_tenants_vat_collection
                    CHECK (vat_status_collection_code = 'vatStatuses'),
                CONSTRAINT fk_tenants_type
                    FOREIGN KEY (type_collection_code, type_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_tenants_status
                    FOREIGN KEY (status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_tenants_currency
                    FOREIGN KEY (currency_collection_code, currency_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_tenants_vat_status
                    FOREIGN KEY (vat_status_collection_code, vat_status_code)
                    REFERENCES governance.master_data_items (collection_code, code)
            );

            CREATE INDEX ix_tenants_status_name
                ON commercial.tenants (status_code, trading_name, id);

            CREATE TABLE commercial.users (
                id uuid PRIMARY KEY,
                email varchar(320) NOT NULL,
                display_name varchar(200) NOT NULL,
                phone varchar(50),
                status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                status_code varchar(100) NOT NULL,
                mfa_enabled boolean NOT NULL,
                last_login_at_utc timestamptz,
                version bigint NOT NULL,
                created_at_utc timestamptz NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                CONSTRAINT ux_users_email UNIQUE (email),
                CONSTRAINT ck_users_email_normalized CHECK (email = lower(email)),
                CONSTRAINT ck_users_version CHECK (version > 0),
                CONSTRAINT ck_users_status_collection
                    CHECK (status_collection_code = 'lifecycleStatuses'),
                CONSTRAINT fk_users_status
                    FOREIGN KEY (status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code)
            );

            CREATE INDEX ix_users_status_name
                ON commercial.users (status_code, display_name, id);

            CREATE TABLE commercial.memberships (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                user_id uuid NOT NULL,
                role_collection_code varchar(100) NOT NULL DEFAULT 'roles',
                role_code varchar(100) NOT NULL,
                status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                status_code varchar(100) NOT NULL,
                invited_by uuid,
                invited_at_utc timestamptz NOT NULL,
                accepted_at_utc timestamptz,
                version bigint NOT NULL,
                created_at_utc timestamptz NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                CONSTRAINT ux_memberships_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_memberships_tenant_user UNIQUE (tenant_id, user_id),
                CONSTRAINT ck_memberships_version CHECK (version > 0),
                CONSTRAINT ck_memberships_role_collection
                    CHECK (role_collection_code = 'roles'),
                CONSTRAINT ck_memberships_status_collection
                    CHECK (status_collection_code = 'lifecycleStatuses'),
                CONSTRAINT fk_memberships_tenant
                    FOREIGN KEY (tenant_id) REFERENCES commercial.tenants (id),
                CONSTRAINT fk_memberships_user
                    FOREIGN KEY (user_id) REFERENCES commercial.users (id),
                CONSTRAINT fk_memberships_inviter
                    FOREIGN KEY (invited_by) REFERENCES commercial.users (id),
                CONSTRAINT fk_memberships_role
                    FOREIGN KEY (role_collection_code, role_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_memberships_status
                    FOREIGN KEY (status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code)
            );

            CREATE INDEX ix_memberships_tenant_status_time
                ON commercial.memberships (tenant_id, status_code, updated_at_utc, id);
            CREATE INDEX ix_memberships_user_status
                ON commercial.memberships (user_id, status_code, tenant_id);
            """);
    }
}
