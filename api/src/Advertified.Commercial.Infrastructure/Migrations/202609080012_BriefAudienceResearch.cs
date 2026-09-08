using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609080012_BriefAudienceResearch")]
public sealed class BriefAudienceResearch : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE commercial.brief_versions
          ADD COLUMN audience_research_json jsonb NOT NULL DEFAULT '[]'::jsonb
          CHECK (jsonb_typeof(audience_research_json) = 'array');
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Approved research evidence must be retained.");
}
