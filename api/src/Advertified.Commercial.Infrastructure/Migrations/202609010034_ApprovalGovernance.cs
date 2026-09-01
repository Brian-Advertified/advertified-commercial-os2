using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609010034_ApprovalGovernance")]
public sealed class ApprovalGovernance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.commercial_policy_versions
                ADD COLUMN allow_self_approval boolean NOT NULL DEFAULT FALSE;

            ALTER TABLE commercial.brief_versions
                ADD COLUMN approval_mode_collection_code varchar(100)
                    NOT NULL DEFAULT 'approvalModes',
                ADD COLUMN approval_mode_code varchar(100),
                ADD CONSTRAINT ck_brief_approval_mode_collection CHECK (
                    approval_mode_collection_code = 'approvalModes'),
                ADD CONSTRAINT fk_brief_approval_mode FOREIGN KEY (
                    approval_mode_collection_code, approval_mode_code)
                    REFERENCES governance.master_data_items (collection_code, code);

            ALTER TABLE commercial.proposal_versions
                ADD COLUMN approval_mode_collection_code varchar(100)
                    NOT NULL DEFAULT 'approvalModes',
                ADD COLUMN approval_mode_code varchar(100),
                ADD COLUMN approval_assignee_user_id uuid,
                ADD COLUMN approval_requested_by uuid,
                ADD COLUMN approval_requested_at_utc timestamptz,
                ADD COLUMN approval_rejected_by uuid,
                ADD COLUMN approval_rejected_at_utc timestamptz,
                ADD COLUMN approval_rejection_reason varchar(1000),
                ADD CONSTRAINT ck_proposal_approval_mode_collection CHECK (
                    approval_mode_collection_code = 'approvalModes'),
                ADD CONSTRAINT ck_proposal_approval_assignment CHECK (
                    (approval_assignee_user_id IS NULL
                     AND approval_requested_by IS NULL
                     AND approval_requested_at_utc IS NULL)
                    OR
                    (approval_assignee_user_id IS NOT NULL
                     AND approval_requested_by IS NOT NULL
                     AND approval_requested_at_utc IS NOT NULL
                     AND approval_assignee_user_id <> approval_requested_by)),
                ADD CONSTRAINT ck_proposal_approval_rejection CHECK (
                    (approval_rejected_by IS NULL
                     AND approval_rejected_at_utc IS NULL
                     AND approval_rejection_reason IS NULL)
                    OR
                    (approval_rejected_by IS NOT NULL
                     AND approval_rejected_at_utc IS NOT NULL
                     AND btrim(approval_rejection_reason) <> '')),
                ADD CONSTRAINT fk_proposal_approval_mode FOREIGN KEY (
                    approval_mode_collection_code, approval_mode_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                ADD CONSTRAINT fk_proposal_approval_assignee FOREIGN KEY (
                    approval_assignee_user_id) REFERENCES commercial.users (id),
                ADD CONSTRAINT fk_proposal_approval_rejector FOREIGN KEY (
                    approval_rejected_by) REFERENCES commercial.users (id),
                ADD CONSTRAINT fk_proposal_approval_requester FOREIGN KEY (
                    approval_requested_by) REFERENCES commercial.users (id);

            CREATE INDEX ix_proposal_approval_assignee
                ON commercial.proposal_versions (
                    tenant_id, approval_assignee_user_id, status_code)
                WHERE approval_assignee_user_id IS NOT NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP INDEX IF EXISTS commercial.ix_proposal_approval_assignee;
            ALTER TABLE commercial.proposal_versions
                DROP CONSTRAINT IF EXISTS fk_proposal_approval_requester,
                DROP CONSTRAINT IF EXISTS fk_proposal_approval_rejector,
                DROP CONSTRAINT IF EXISTS fk_proposal_approval_assignee,
                DROP CONSTRAINT IF EXISTS fk_proposal_approval_mode,
                DROP CONSTRAINT IF EXISTS ck_proposal_approval_rejection,
                DROP CONSTRAINT IF EXISTS ck_proposal_approval_assignment,
                DROP CONSTRAINT IF EXISTS ck_proposal_approval_mode_collection,
                DROP COLUMN IF EXISTS approval_rejection_reason,
                DROP COLUMN IF EXISTS approval_rejected_at_utc,
                DROP COLUMN IF EXISTS approval_rejected_by,
                DROP COLUMN IF EXISTS approval_requested_at_utc,
                DROP COLUMN IF EXISTS approval_requested_by,
                DROP COLUMN IF EXISTS approval_assignee_user_id,
                DROP COLUMN IF EXISTS approval_mode_code,
                DROP COLUMN IF EXISTS approval_mode_collection_code;
            ALTER TABLE commercial.brief_versions
                DROP CONSTRAINT IF EXISTS fk_brief_approval_mode,
                DROP CONSTRAINT IF EXISTS ck_brief_approval_mode_collection,
                DROP COLUMN IF EXISTS approval_mode_code,
                DROP COLUMN IF EXISTS approval_mode_collection_code;
            ALTER TABLE commercial.commercial_policy_versions
                DROP COLUMN IF EXISTS allow_self_approval;
            """);
    }
}
