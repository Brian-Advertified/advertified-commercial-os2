using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202608300016_CommercialPolicy")]
public sealed class CommercialPolicy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE commercial.commercial_policies (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                current_version_id uuid,
                version bigint NOT NULL,
                created_at_utc timestamptz NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_commercial_policy_tenant UNIQUE (tenant_id),
                CONSTRAINT ux_commercial_policy_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ck_commercial_policy_version CHECK (version > 0),
                CONSTRAINT fk_commercial_policy_tenant FOREIGN KEY (tenant_id)
                    REFERENCES commercial.tenants (id)
            );

            CREATE TABLE commercial.commercial_policy_versions (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                policy_id uuid NOT NULL,
                version_number integer NOT NULL,
                markup_basis_points integer NOT NULL,
                management_fee_basis_points integer NOT NULL,
                commission_basis_points integer NOT NULL,
                vat_status_collection_code varchar(100) NOT NULL DEFAULT 'vatStatuses',
                vat_status_code varchar(100) NOT NULL,
                vat_rate_basis_points integer NOT NULL,
                prices_include_vat boolean NOT NULL,
                currency_collection_code varchar(100) NOT NULL DEFAULT 'currencies',
                currency_code varchar(100) NOT NULL,
                booking_approval_threshold_minor bigint NOT NULL,
                created_by uuid NOT NULL,
                created_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_commercial_policy_version_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_commercial_policy_version_number UNIQUE (
                    tenant_id, policy_id, version_number),
                CONSTRAINT ck_commercial_policy_version_number CHECK (version_number > 0),
                CONSTRAINT ck_commercial_policy_basis_points CHECK (
                    markup_basis_points BETWEEN 0 AND 100000
                    AND management_fee_basis_points BETWEEN 0 AND 100000
                    AND commission_basis_points BETWEEN 0 AND 10000
                    AND vat_rate_basis_points BETWEEN 0 AND 10000),
                CONSTRAINT ck_commercial_policy_threshold CHECK (
                    booking_approval_threshold_minor >= 0),
                CONSTRAINT ck_commercial_policy_vat_collection CHECK (
                    vat_status_collection_code = 'vatStatuses'),
                CONSTRAINT ck_commercial_policy_currency_collection CHECK (
                    currency_collection_code = 'currencies'),
                CONSTRAINT ck_commercial_policy_vat_treatment CHECK (
                    (vat_status_code = 'REGISTERED' AND vat_rate_basis_points > 0)
                    OR (vat_status_code IN ('EXEMPT', 'NOT_APPLICABLE')
                        AND vat_rate_basis_points = 0 AND prices_include_vat = false)),
                CONSTRAINT fk_commercial_policy_version_policy FOREIGN KEY (
                    tenant_id, policy_id)
                    REFERENCES commercial.commercial_policies (tenant_id, id),
                CONSTRAINT fk_commercial_policy_version_vat FOREIGN KEY (
                    vat_status_collection_code, vat_status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_commercial_policy_version_currency FOREIGN KEY (
                    currency_collection_code, currency_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_commercial_policy_version_creator FOREIGN KEY (created_by)
                    REFERENCES commercial.users (id)
            );

            ALTER TABLE commercial.commercial_policies
                ADD CONSTRAINT fk_commercial_policy_current_version FOREIGN KEY (
                    tenant_id, current_version_id)
                    REFERENCES commercial.commercial_policy_versions (tenant_id, id);

            ALTER TABLE commercial.commercial_policies ENABLE ROW LEVEL SECURITY;
            ALTER TABLE commercial.commercial_policies FORCE ROW LEVEL SECURITY;
            CREATE POLICY commercial_policies_tenant_scope
                ON commercial.commercial_policies
                USING (tenant_id = commercial.current_tenant_id())
                WITH CHECK (tenant_id = commercial.current_tenant_id());

            ALTER TABLE commercial.commercial_policy_versions ENABLE ROW LEVEL SECURITY;
            ALTER TABLE commercial.commercial_policy_versions FORCE ROW LEVEL SECURITY;
            CREATE POLICY commercial_policy_versions_tenant_scope
                ON commercial.commercial_policy_versions
                USING (tenant_id = commercial.current_tenant_id())
                WITH CHECK (tenant_id = commercial.current_tenant_id());

            CREATE TRIGGER protect_commercial_policy_versions
                BEFORE UPDATE OR DELETE ON commercial.commercial_policy_versions
                FOR EACH ROW EXECUTE FUNCTION commercial.reject_immutable_record_change();

            GRANT SELECT, INSERT, UPDATE ON commercial.commercial_policies TO advertified_app;
            GRANT SELECT, INSERT ON commercial.commercial_policy_versions TO advertified_app;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.commercial_policies
                DROP CONSTRAINT IF EXISTS fk_commercial_policy_current_version;
            DROP TABLE IF EXISTS commercial.commercial_policy_versions;
            DROP TABLE IF EXISTS commercial.commercial_policies;
            """);
    }
}
