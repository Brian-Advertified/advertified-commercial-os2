using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609010028_BriefReadiness")]
public sealed class BriefReadiness : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.campaign_briefs
                ADD COLUMN ready_version_id uuid;

            UPDATE commercial.campaign_briefs
            SET ready_version_id = approved_version_id
            WHERE approved_version_id IS NOT NULL;

            ALTER TABLE commercial.campaign_briefs
                ADD CONSTRAINT fk_campaign_briefs_ready_version
                FOREIGN KEY (tenant_id, ready_version_id)
                REFERENCES commercial.brief_versions (tenant_id, id);
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
                    FROM commercial.brief_versions
                    WHERE status_code = 'READY'
                ) THEN
                    RAISE EXCEPTION
                        'Cannot remove Brief readiness while READY Brief versions exist.';
                END IF;
            END
            $$;

            ALTER TABLE commercial.campaign_briefs
                DROP CONSTRAINT IF EXISTS fk_campaign_briefs_ready_version;
            ALTER TABLE commercial.campaign_briefs
                DROP COLUMN IF EXISTS ready_version_id;
            """);
    }
}
