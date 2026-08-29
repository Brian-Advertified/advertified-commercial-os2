using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202608290008_ProposalClientDecision")]
public sealed partial class ProposalClientDecision : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        CreateProposalTables(migrationBuilder);
        CreateProposalSecurityBoundary(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP POLICY IF EXISTS proposal_preparer_user_directory ON commercial.users;
            DROP TABLE IF EXISTS commercial.proposal_decisions;
            DROP TABLE IF EXISTS commercial.proposal_documents;
            DROP TABLE IF EXISTS commercial.proposal_options;
            DROP TABLE IF EXISTS commercial.proposal_versions;
            """);
    }
}
