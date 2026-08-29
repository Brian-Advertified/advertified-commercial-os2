using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class CanonicalCommercialFoundation
{
    private static void CreateTenantOwnedTables(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE commercial.client_accounts (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                external_reference varchar(100) NOT NULL,
                legal_name varchar(200) NOT NULL,
                trading_name varchar(200) NOT NULL,
                website varchar(2048),
                industry varchar(100),
                billing_profile_json jsonb NOT NULL,
                primary_contact_id uuid,
                status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                status_code varchar(100) NOT NULL,
                version bigint NOT NULL,
                created_at_utc timestamptz NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                CONSTRAINT ux_client_accounts_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_client_accounts_tenant_external_ref
                    UNIQUE (tenant_id, external_reference),
                CONSTRAINT ck_client_accounts_version CHECK (version > 0),
                CONSTRAINT ck_client_accounts_status_collection
                    CHECK (status_collection_code = 'lifecycleStatuses'),
                CONSTRAINT fk_client_accounts_tenant
                    FOREIGN KEY (tenant_id) REFERENCES commercial.tenants (id),
                CONSTRAINT fk_client_accounts_status
                    FOREIGN KEY (status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code)
            );

            CREATE INDEX ix_client_accounts_tenant_status_name
                ON commercial.client_accounts
                (tenant_id, status_code, trading_name, id);

            CREATE TABLE commercial.agencies (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                external_reference varchar(100) NOT NULL,
                legal_name varchar(200) NOT NULL,
                trading_name varchar(200) NOT NULL,
                website varchar(2048),
                status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                status_code varchar(100) NOT NULL,
                version bigint NOT NULL,
                created_at_utc timestamptz NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                CONSTRAINT ux_agencies_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_agencies_tenant_external_ref
                    UNIQUE (tenant_id, external_reference),
                CONSTRAINT ck_agencies_version CHECK (version > 0),
                CONSTRAINT ck_agencies_status_collection
                    CHECK (status_collection_code = 'lifecycleStatuses'),
                CONSTRAINT fk_agencies_tenant
                    FOREIGN KEY (tenant_id) REFERENCES commercial.tenants (id),
                CONSTRAINT fk_agencies_status
                    FOREIGN KEY (status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code)
            );

            CREATE INDEX ix_agencies_tenant_status_name
                ON commercial.agencies (tenant_id, status_code, trading_name, id);

            CREATE TABLE commercial.contacts (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                client_account_id uuid NOT NULL,
                name varchar(200) NOT NULL,
                job_title varchar(100),
                email varchar(320) NOT NULL,
                phone varchar(50),
                purpose_collection_code varchar(100) NOT NULL DEFAULT 'contactPurposes',
                purpose_code varchar(100) NOT NULL,
                consent_basis varchar(500) NOT NULL,
                retain_until date,
                status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                status_code varchar(100) NOT NULL,
                version bigint NOT NULL,
                created_at_utc timestamptz NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                CONSTRAINT ux_contacts_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ck_contacts_version CHECK (version > 0),
                CONSTRAINT ck_contacts_email_normalized CHECK (email = lower(email)),
                CONSTRAINT ck_contacts_purpose_collection
                    CHECK (purpose_collection_code = 'contactPurposes'),
                CONSTRAINT ck_contacts_status_collection
                    CHECK (status_collection_code = 'lifecycleStatuses'),
                CONSTRAINT fk_contacts_tenant
                    FOREIGN KEY (tenant_id) REFERENCES commercial.tenants (id),
                CONSTRAINT fk_contacts_tenant_client_account
                    FOREIGN KEY (tenant_id, client_account_id)
                    REFERENCES commercial.client_accounts (tenant_id, id),
                CONSTRAINT fk_contacts_purpose
                    FOREIGN KEY (purpose_collection_code, purpose_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_contacts_status
                    FOREIGN KEY (status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code)
            );

            CREATE INDEX ix_contacts_tenant_status_name
                ON commercial.contacts (tenant_id, status_code, name, id);
            CREATE INDEX "IX_contacts_tenant_id_client_account_id"
                ON commercial.contacts (tenant_id, client_account_id);

            ALTER TABLE commercial.client_accounts
                ADD CONSTRAINT fk_client_accounts_primary_contact
                FOREIGN KEY (tenant_id, primary_contact_id)
                REFERENCES commercial.contacts (tenant_id, id);
            """);
    }
}
