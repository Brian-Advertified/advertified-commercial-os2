using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609080008_AudienceStrategyApproval")]
public sealed class AudienceStrategyApproval : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TRIGGER protect_audience_definition_sets
            ON commercial.audience_definition_sets;
        ALTER TABLE commercial.audience_definition_sets
            DISABLE ROW LEVEL SECURITY;

        ALTER TABLE commercial.audience_definition_sets
            ADD COLUMN approved_by uuid,
            ADD COLUMN approved_at_utc timestamp with time zone,
            ADD COLUMN version bigint NOT NULL DEFAULT 1;

        UPDATE commercial.audience_definition_sets
        SET approved_by = created_by,
            approved_at_utc = created_at_utc
        WHERE status_code = 'APPROVED';

        ALTER TABLE commercial.audience_definition_sets
            ADD CONSTRAINT ck_audience_definition_sets_version
                CHECK (version > 0),
            ADD CONSTRAINT ck_audience_definition_sets_approval_shape
                CHECK (
                    (status_code = 'APPROVED' AND approved_by IS NOT NULL
                        AND approved_at_utc IS NOT NULL)
                    OR
                    (status_code <> 'APPROVED' AND approved_by IS NULL
                        AND approved_at_utc IS NULL)
                );

        ALTER TABLE commercial.audience_definition_sets
            ENABLE ROW LEVEL SECURITY;
        ALTER TABLE commercial.audience_definition_sets
            FORCE ROW LEVEL SECURITY;

        CREATE TRIGGER protect_audience_definition_sets
            BEFORE DELETE OR UPDATE ON commercial.audience_definition_sets
            FOR EACH ROW EXECUTE FUNCTION commercial.reject_immutable_record_change();
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE commercial.audience_definition_sets
            DROP CONSTRAINT ck_audience_definition_sets_approval_shape,
            DROP CONSTRAINT ck_audience_definition_sets_version,
            DROP COLUMN version,
            DROP COLUMN approved_at_utc,
            DROP COLUMN approved_by;
        """);
}
