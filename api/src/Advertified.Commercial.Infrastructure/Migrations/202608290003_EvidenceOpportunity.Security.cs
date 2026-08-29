using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class EvidenceOpportunity
{
    private static readonly string[] ForcedTenantTables =
    [
        "opportunities",
        "client_account_assignments",
        "evidence_sources",
        "opportunity_evidence_sources",
        "evidence_items",
        "evidence_sets",
        "evidence_set_items",
        "business_interpretations",
        "opportunity_angle_sets",
        "opportunity_angles",
        "strategy_versions",
        "critic_reports",
        "critic_objections",
        "human_tasks",
    ];

    private static readonly string[] WorkerOwnedTables =
    [
        "agent_runs",
        "agent_run_steps",
        "ai_usage_ledger",
    ];

    private static void CreateSecurityBoundary(MigrationBuilder migrationBuilder)
    {
        foreach (var table in ForcedTenantTables)
        {
            migrationBuilder.Sql($"""
                ALTER TABLE commercial.{table} ENABLE ROW LEVEL SECURITY;
                ALTER TABLE commercial.{table} FORCE ROW LEVEL SECURITY;
                CREATE POLICY {table}_tenant_scope ON commercial.{table}
                    USING (tenant_id = commercial.current_tenant_id())
                    WITH CHECK (tenant_id = commercial.current_tenant_id());
                """);
        }

        foreach (var table in WorkerOwnedTables)
        {
            migrationBuilder.Sql($"""
                ALTER TABLE commercial.{table} ENABLE ROW LEVEL SECURITY;
                CREATE POLICY {table}_tenant_scope ON commercial.{table}
                    USING (tenant_id = commercial.current_tenant_id())
                    WITH CHECK (tenant_id = commercial.current_tenant_id());
                """);
        }

        migrationBuilder.Sql(
            """
            CREATE TRIGGER protect_evidence_sources
                BEFORE UPDATE OR DELETE ON commercial.evidence_sources
                FOR EACH ROW EXECUTE FUNCTION commercial.reject_immutable_record_change();

            CREATE TRIGGER protect_critic_reports
                BEFORE UPDATE OR DELETE ON commercial.critic_reports
                FOR EACH ROW EXECUTE FUNCTION commercial.reject_immutable_record_change();

            CREATE FUNCTION commercial.reject_final_artifact_change()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            BEGIN
                IF OLD.status_code = 'APPROVED' THEN
                    RAISE EXCEPTION 'Approved commercial artefacts are immutable';
                END IF;
                RETURN NEW;
            END
            $$;

            CREATE TRIGGER protect_approved_evidence_sets
                BEFORE UPDATE OR DELETE ON commercial.evidence_sets
                FOR EACH ROW EXECUTE FUNCTION commercial.reject_final_artifact_change();
            CREATE TRIGGER protect_approved_interpretations
                BEFORE UPDATE OR DELETE ON commercial.business_interpretations
                FOR EACH ROW EXECUTE FUNCTION commercial.reject_final_artifact_change();
            CREATE TRIGGER protect_approved_strategies
                BEFORE UPDATE OR DELETE ON commercial.strategy_versions
                FOR EACH ROW EXECUTE FUNCTION commercial.reject_final_artifact_change();

            CREATE FUNCTION commercial.claim_next_agent_run(
                worker_id uuid,
                claimed_at_utc timestamptz,
                lease_until_utc timestamptz)
            RETURNS TABLE (tenant_id uuid, run_id uuid, requested_by uuid)
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, commercial
            AS $$
            BEGIN
                RETURN QUERY
                WITH candidate AS (
                    SELECT candidate_run.id
                    FROM commercial.agent_runs candidate_run
                    WHERE (
                        candidate_run.status_code = 'QUEUED'
                        OR (
                            candidate_run.status_code = 'RUNNING'
                            AND candidate_run.lease_expires_at_utc < claimed_at_utc
                        )
                    )
                    AND (
                        candidate_run.next_attempt_at_utc IS NULL
                        OR candidate_run.next_attempt_at_utc <= claimed_at_utc
                    )
                    ORDER BY candidate_run.created_at_utc, candidate_run.id
                    FOR UPDATE SKIP LOCKED
                    LIMIT 1
                )
                UPDATE commercial.agent_runs claimed
                SET status_code = 'RUNNING',
                    lease_owner = worker_id,
                    lease_expires_at_utc = lease_until_utc,
                    attempts = claimed.attempts + 1,
                    version = claimed.version + 1,
                    updated_at_utc = claimed_at_utc
                FROM candidate
                WHERE claimed.id = candidate.id
                RETURNING claimed.tenant_id, claimed.id, claimed.requested_by;
            END
            $$;

            REVOKE ALL ON FUNCTION commercial.claim_next_agent_run(uuid, timestamptz, timestamptz)
                FROM PUBLIC;
            GRANT EXECUTE ON FUNCTION commercial.claim_next_agent_run(uuid, timestamptz, timestamptz)
                TO advertified_app;

            GRANT SELECT, INSERT, UPDATE ON
                commercial.opportunities,
                commercial.client_account_assignments,
                commercial.evidence_sources,
                commercial.opportunity_evidence_sources,
                commercial.evidence_items,
                commercial.evidence_sets,
                commercial.evidence_set_items,
                commercial.business_interpretations,
                commercial.opportunity_angle_sets,
                commercial.opportunity_angles,
                commercial.strategy_versions,
                commercial.critic_reports,
                commercial.critic_objections,
                commercial.agent_runs,
                commercial.agent_run_steps,
                commercial.ai_usage_ledger,
                commercial.human_tasks
                TO advertified_app;
            """);
    }
}
