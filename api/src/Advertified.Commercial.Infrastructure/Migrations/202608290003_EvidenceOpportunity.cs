using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202608290003_EvidenceOpportunity")]
public sealed partial class EvidenceOpportunity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        CreateOpportunityTables(migrationBuilder);
        CreateEvidenceTables(migrationBuilder);
        CreateRunTables(migrationBuilder);
        CreateStrategyTables(migrationBuilder);
        CreateSecurityBoundary(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP FUNCTION IF EXISTS commercial.claim_next_agent_run(uuid, timestamptz, timestamptz);
            DROP TABLE IF EXISTS commercial.critic_objections;
            DROP TABLE IF EXISTS commercial.critic_reports;
            DROP TABLE IF EXISTS commercial.strategy_versions;
            DROP TABLE IF EXISTS commercial.opportunity_angles;
            DROP TABLE IF EXISTS commercial.opportunity_angle_sets;
            DROP TABLE IF EXISTS commercial.business_interpretations;
            DROP TABLE IF EXISTS commercial.ai_usage_ledger;
            DROP TABLE IF EXISTS commercial.agent_run_steps;
            DROP TABLE IF EXISTS commercial.human_tasks;
            DROP TABLE IF EXISTS commercial.agent_runs;
            DROP TABLE IF EXISTS commercial.evidence_set_items;
            DROP TABLE IF EXISTS commercial.evidence_sets;
            DROP TABLE IF EXISTS commercial.evidence_items;
            DROP TABLE IF EXISTS commercial.opportunity_evidence_sources;
            DROP TABLE IF EXISTS commercial.evidence_sources;
            DROP TABLE IF EXISTS commercial.client_account_assignments;
            DROP TABLE IF EXISTS commercial.opportunities;
            DROP FUNCTION IF EXISTS commercial.reject_final_artifact_change();
            """);
    }
}
