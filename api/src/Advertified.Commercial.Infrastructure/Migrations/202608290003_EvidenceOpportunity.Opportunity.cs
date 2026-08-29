using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class EvidenceOpportunity
{
    private static void CreateOpportunityTables(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE commercial.opportunities (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                client_account_id uuid NOT NULL,
                title varchar(200) NOT NULL,
                source_type_collection_code varchar(100) NOT NULL DEFAULT 'opportunitySourceTypes',
                source_type_code varchar(100) NOT NULL,
                source_ref varchar(2048),
                owner_user_id uuid NOT NULL,
                stage_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                stage_code varchar(100) NOT NULL,
                expected_value_minor bigint,
                currency_collection_code varchar(100) NOT NULL DEFAULT 'currencies',
                currency_code varchar(3),
                deadline date,
                problem_summary varchar(2000),
                objective_summary varchar(2000),
                version bigint NOT NULL,
                created_at_utc timestamptz NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                CONSTRAINT ux_opportunities_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ck_opportunities_version CHECK (version > 0),
                CONSTRAINT ck_opportunities_value CHECK (expected_value_minor IS NULL OR expected_value_minor >= 0),
                CONSTRAINT ck_opportunities_source_collection CHECK (source_type_collection_code = 'opportunitySourceTypes'),
                CONSTRAINT ck_opportunities_stage_collection CHECK (stage_collection_code = 'lifecycleStatuses'),
                CONSTRAINT ck_opportunities_currency_collection CHECK (currency_collection_code = 'currencies'),
                CONSTRAINT fk_opportunities_tenant FOREIGN KEY (tenant_id) REFERENCES commercial.tenants (id),
                CONSTRAINT fk_opportunities_client FOREIGN KEY (tenant_id, client_account_id)
                    REFERENCES commercial.client_accounts (tenant_id, id),
                CONSTRAINT fk_opportunities_owner FOREIGN KEY (owner_user_id) REFERENCES commercial.users (id),
                CONSTRAINT fk_opportunities_source_type FOREIGN KEY (source_type_collection_code, source_type_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_opportunities_stage FOREIGN KEY (stage_collection_code, stage_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_opportunities_currency FOREIGN KEY (currency_collection_code, currency_code)
                    REFERENCES governance.master_data_items (collection_code, code)
            );

            CREATE INDEX ix_opportunities_tenant_stage_updated
                ON commercial.opportunities (tenant_id, stage_code, updated_at_utc DESC, id);
            CREATE INDEX ix_opportunities_tenant_owner
                ON commercial.opportunities (tenant_id, owner_user_id, updated_at_utc DESC);

            CREATE TABLE commercial.client_account_assignments (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                client_account_id uuid NOT NULL,
                user_id uuid NOT NULL,
                effective_from_utc timestamptz NOT NULL,
                effective_to_utc timestamptz,
                assigned_by uuid NOT NULL,
                created_at_utc timestamptz NOT NULL,
                CONSTRAINT ux_client_assignments_scope UNIQUE (tenant_id, client_account_id, user_id),
                CONSTRAINT ck_client_assignments_dates CHECK (
                    effective_to_utc IS NULL OR effective_to_utc > effective_from_utc),
                CONSTRAINT fk_client_assignments_tenant FOREIGN KEY (tenant_id) REFERENCES commercial.tenants (id),
                CONSTRAINT fk_client_assignments_client FOREIGN KEY (tenant_id, client_account_id)
                    REFERENCES commercial.client_accounts (tenant_id, id),
                CONSTRAINT fk_client_assignments_user FOREIGN KEY (user_id) REFERENCES commercial.users (id),
                CONSTRAINT fk_client_assignments_actor FOREIGN KEY (assigned_by) REFERENCES commercial.users (id)
            );

            CREATE INDEX ix_client_assignments_user_active
                ON commercial.client_account_assignments
                (tenant_id, user_id, effective_from_utc, effective_to_utc);
            """);
    }
}
