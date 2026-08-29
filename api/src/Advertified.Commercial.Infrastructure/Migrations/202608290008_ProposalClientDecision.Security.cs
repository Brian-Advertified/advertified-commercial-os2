using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class ProposalClientDecision
{
    private static readonly string[] ProposalTenantTables =
    [
        "proposal_versions",
        "proposal_options",
        "proposal_documents",
        "proposal_decisions",
    ];

    private static readonly string[] ImmutableProposalTables =
    [
        "proposal_documents",
        "proposal_decisions",
    ];

    private static void CreateProposalSecurityBoundary(MigrationBuilder migrationBuilder)
    {
        foreach (var table in ProposalTenantTables)
        {
            migrationBuilder.Sql($"""
                ALTER TABLE commercial.{table} ENABLE ROW LEVEL SECURITY;
                ALTER TABLE commercial.{table} FORCE ROW LEVEL SECURITY;
                CREATE POLICY {table}_tenant_scope ON commercial.{table}
                    USING (tenant_id = commercial.current_tenant_id())
                    WITH CHECK (tenant_id = commercial.current_tenant_id());
                """);
        }
        foreach (var table in ImmutableProposalTables)
        {
            migrationBuilder.Sql($"""
                CREATE TRIGGER protect_{table}
                    BEFORE UPDATE OR DELETE ON commercial.{table}
                    FOR EACH ROW EXECUTE FUNCTION commercial.reject_immutable_record_change();
                """);
        }
        migrationBuilder.Sql(
            """
            CREATE POLICY proposal_preparer_user_directory ON commercial.users
                FOR SELECT
                USING (
                    id = commercial.current_user_id()
                    OR EXISTS (
                        SELECT 1
                        FROM commercial.memberships actor_membership
                        JOIN commercial.memberships recipient_membership
                          ON recipient_membership.tenant_id = actor_membership.tenant_id
                         AND recipient_membership.user_id = users.id
                        WHERE actor_membership.tenant_id = commercial.current_tenant_id()
                          AND actor_membership.user_id = commercial.current_user_id()
                          AND actor_membership.status_code = 'ACTIVE'
                          AND recipient_membership.status_code = 'ACTIVE'
                          AND actor_membership.role_code IN (
                              'platform_admin', 'internal_planner',
                              'agency_admin', 'agency_campaign_user')
                          AND recipient_membership.role_code IN (
                              'advertiser_admin', 'advertiser_approver')
                    )
                );

            GRANT SELECT, INSERT, UPDATE ON
                commercial.proposal_versions,
                commercial.proposal_options,
                commercial.proposal_documents,
                commercial.proposal_decisions
                TO advertified_app;
            """);
    }
}
