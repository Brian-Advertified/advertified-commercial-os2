using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202608290004_CanonicalBrief")]
public sealed partial class CanonicalBrief : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        CreateBriefTables(migrationBuilder);
        CreateBriefSecurityBoundary(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DELETE FROM commercial.human_tasks
            WHERE task_type_code = 'BRIEF_APPROVAL';
            ALTER TABLE commercial.human_tasks ALTER COLUMN opportunity_id SET NOT NULL;
            ALTER TABLE commercial.campaign_briefs
                DROP CONSTRAINT IF EXISTS fk_campaign_briefs_current_draft;
            ALTER TABLE commercial.campaign_briefs
                DROP CONSTRAINT IF EXISTS fk_campaign_briefs_approved_version;
            DROP TABLE IF EXISTS commercial.brief_version_evidence_items;
            DROP TABLE IF EXISTS commercial.brief_versions;
            DROP TABLE IF EXISTS commercial.brief_sources;
            DROP TABLE IF EXISTS commercial.campaign_briefs;
            DROP FUNCTION IF EXISTS commercial.reject_submitted_brief_content_change();
            """);
    }
}
