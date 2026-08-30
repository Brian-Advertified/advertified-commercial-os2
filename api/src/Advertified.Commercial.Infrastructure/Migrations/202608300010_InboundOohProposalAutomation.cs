using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202608300010_InboundOohProposalAutomation")]
public sealed partial class InboundOohProposalAutomation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        CreateEmailAutomationTables(migrationBuilder);
        CreateEmailAutomationSecurity(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TABLE IF EXISTS commercial.email_proposal_automation_runs;
            DROP TABLE IF EXISTS commercial.inbound_email_attachments;
            DROP TABLE IF EXISTS commercial.inbound_campaign_emails;
            DROP TABLE IF EXISTS commercial.inbound_mailboxes;
            """);
    }
}
