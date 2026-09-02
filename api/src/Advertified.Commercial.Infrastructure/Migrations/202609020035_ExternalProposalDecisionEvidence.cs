using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609020035_ExternalProposalDecisionEvidence")]
public sealed class ExternalProposalDecisionEvidence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.proposal_decisions
                ADD COLUMN recorded_for_external_party boolean NOT NULL DEFAULT FALSE,
                ADD COLUMN external_party_email varchar(320),
                ADD COLUMN evidence_reference varchar(1000),
                ADD CONSTRAINT ck_proposal_decision_external_evidence CHECK (
                    (recorded_for_external_party = FALSE
                     AND external_party_email IS NULL
                     AND evidence_reference IS NULL)
                    OR
                    (recorded_for_external_party = TRUE
                     AND btrim(external_party_email) <> ''
                     AND btrim(evidence_reference) <> ''));

            CREATE INDEX ix_proposal_decision_external_party
                ON commercial.proposal_decisions (
                    tenant_id, proposal_version_id, recorded_for_external_party);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP INDEX IF EXISTS commercial.ix_proposal_decision_external_party;
            ALTER TABLE commercial.proposal_decisions
                DROP CONSTRAINT IF EXISTS ck_proposal_decision_external_evidence,
                DROP COLUMN IF EXISTS evidence_reference,
                DROP COLUMN IF EXISTS external_party_email,
                DROP COLUMN IF EXISTS recorded_for_external_party;
            """);
    }
}
