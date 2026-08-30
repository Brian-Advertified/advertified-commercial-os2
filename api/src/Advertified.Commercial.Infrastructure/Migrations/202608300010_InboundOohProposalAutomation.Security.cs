using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class InboundOohProposalAutomation
{
    private static readonly string[] EmailAutomationTables =
    [
        "inbound_mailboxes",
        "inbound_campaign_emails",
        "inbound_email_attachments",
        "email_proposal_automation_runs",
    ];

    private static readonly string[] ImmutableEmailSourceTables =
    [
        "inbound_campaign_emails",
        "inbound_email_attachments",
    ];

    private static void CreateEmailAutomationSecurity(MigrationBuilder migrationBuilder)
    {
        foreach (var table in EmailAutomationTables)
        {
            migrationBuilder.Sql($"""
                ALTER TABLE commercial.{table} ENABLE ROW LEVEL SECURITY;
                ALTER TABLE commercial.{table} FORCE ROW LEVEL SECURITY;
                CREATE POLICY {table}_tenant_scope ON commercial.{table}
                    USING (tenant_id = commercial.current_tenant_id())
                    WITH CHECK (tenant_id = commercial.current_tenant_id());
                """);
        }
        foreach (var table in ImmutableEmailSourceTables)
        {
            migrationBuilder.Sql($"""
                CREATE TRIGGER protect_{table}
                    BEFORE UPDATE OR DELETE ON commercial.{table}
                    FOR EACH ROW EXECUTE FUNCTION commercial.reject_immutable_record_change();
                """);
        }
        migrationBuilder.Sql(
            """
            GRANT SELECT, INSERT, UPDATE ON
                commercial.inbound_mailboxes,
                commercial.email_proposal_automation_runs
                TO advertified_app;
            GRANT SELECT, INSERT ON
                commercial.inbound_campaign_emails,
                commercial.inbound_email_attachments
                TO advertified_app;
            """);
    }
}
