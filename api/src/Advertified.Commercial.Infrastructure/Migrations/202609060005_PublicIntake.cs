using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609060005_PublicIntake")]
public sealed class PublicIntake : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            CREATE TABLE governance.public_intake_requests (
                id uuid PRIMARY KEY,
                type_code varchar(50) NOT NULL,
                name varchar(200) NOT NULL,
                email varchar(320) NOT NULL,
                phone varchar(50),
                organisation varchar(200) NOT NULL,
                website varchar(2048),
                relationship varchar(1000),
                message varchar(4000),
                status_code varchar(100) NOT NULL,
                created_at_utc timestamptz NOT NULL,
                reviewed_by uuid REFERENCES commercial.users(id),
                reviewed_at_utc timestamptz,
                review_reason varchar(1000),
                provisioned_tenant_id uuid REFERENCES commercial.tenants(id),
                provisioned_user_id uuid REFERENCES commercial.users(id),
                version bigint NOT NULL,
                CONSTRAINT ck_public_intake_email_normalized
                    CHECK (email = lower(email)),
                CONSTRAINT ck_public_intake_version CHECK (version > 0),
                CONSTRAINT ck_public_intake_review_shape CHECK (
                    (reviewed_at_utc IS NULL
                        AND reviewed_by IS NULL
                        AND review_reason IS NULL)
                    OR
                    (reviewed_at_utc IS NOT NULL
                        AND reviewed_by IS NOT NULL
                        AND review_reason IS NOT NULL))
            );

            CREATE INDEX ix_public_intake_status_time
                ON governance.public_intake_requests(
                    status_code, created_at_utc DESC, id DESC);
            CREATE INDEX ix_public_intake_email
                ON governance.public_intake_requests(
                    lower(email), created_at_utc DESC);

            ALTER TABLE commercial.inventory_supplier_memberships
                DROP CONSTRAINT ck_inventory_supplier_membership_role;
            ALTER TABLE commercial.inventory_supplier_memberships
                ADD CONSTRAINT ck_inventory_supplier_membership_role
                CHECK (role_code IN ('supplier_user', 'influencer_rep'));

            REVOKE ALL ON TABLE governance.public_intake_requests FROM PUBLIC;
            GRANT SELECT, INSERT, UPDATE
                ON TABLE governance.public_intake_requests TO advertified_app;
            """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM governance.public_intake_requests) THEN
                    RAISE EXCEPTION 'Retained public intake requests must not be discarded';
                END IF;
            END $$;

            DROP TABLE governance.public_intake_requests;
            ALTER TABLE commercial.inventory_supplier_memberships
                DROP CONSTRAINT ck_inventory_supplier_membership_role;
            ALTER TABLE commercial.inventory_supplier_memberships
                ADD CONSTRAINT ck_inventory_supplier_membership_role
                CHECK (role_code = 'supplier_user');
            """);
}
