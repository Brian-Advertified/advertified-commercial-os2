using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609080013_AiOwnerBudgetSafety")]
public sealed class AiOwnerBudgetSafety : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        -- Preserve the existing preflight caller signature, but report the same global
        -- conservative commitments enforced by dispatch; no misleading monthly headroom.
        CREATE OR REPLACE FUNCTION governance.read_ai_monthly_budget(p_month_start date)
        RETURNS bigint LANGUAGE sql STABLE SECURITY DEFINER
        SET search_path = pg_catalog AS $function$
            SELECT COALESCE(sum(maximum_cost_usd_micros), 0)::bigint
            FROM governance.ai_monthly_budget_ledger
        $function$;

        CREATE OR REPLACE FUNCTION governance.reserve_ai_monthly_budget(
            p_month_start date, p_run_id uuid, p_step_id uuid,
            p_tenant_id uuid, p_requested bigint)
        RETURNS boolean LANGUAGE plpgsql VOLATILE SECURITY DEFINER
        SET search_path = pg_catalog AS $function$
        DECLARE committed bigint;
        BEGIN
            IF p_requested <= 0 OR p_requested > 5000000 THEN RETURN false; END IF;
            PERFORM pg_advisory_xact_lock(hashtextextended('advertified:ai:owner-budget', 0));
            -- Dispatch is one-shot: an existing identity is never authority for another call.
            IF EXISTS (SELECT 1 FROM governance.ai_monthly_budget_ledger
                WHERE run_id = p_run_id AND step_id = p_step_id) THEN RETURN false; END IF;
            -- Owner authorised US$5 total, not US$5 per month. Unused reservations remain
            -- committed, including failed/uncertain calls. Actual usage is audit only.
            SELECT COALESCE(sum(maximum_cost_usd_micros), 0)::bigint INTO committed
                FROM governance.ai_monthly_budget_ledger;
            IF p_requested > 5000000 - committed THEN RETURN false; END IF;
            INSERT INTO governance.ai_monthly_budget_ledger VALUES (
                p_month_start, p_run_id, p_step_id, p_tenant_id,
                p_requested, NULL, 'RESERVED', statement_timestamp(), NULL);
            RETURN true;
        END $function$;

        CREATE OR REPLACE FUNCTION governance.complete_ai_monthly_budget(
            p_month_start date, p_run_id uuid, p_step_id uuid, p_actual bigint)
        RETURNS boolean LANGUAGE plpgsql VOLATILE SECURITY DEFINER
        SET search_path = pg_catalog AS $function$
        BEGIN
            PERFORM pg_advisory_xact_lock(hashtextextended('advertified:ai:owner-budget', 0));
            UPDATE governance.ai_monthly_budget_ledger
            SET actual_cost_usd_micros = p_actual,
                status_code = 'COMPLETED', completed_at_utc = statement_timestamp()
            WHERE month_start_utc = p_month_start AND run_id = p_run_id
              AND step_id = p_step_id AND status_code = 'RESERVED'
              AND p_actual BETWEEN 0 AND maximum_cost_usd_micros;
            RETURN FOUND;
        END $function$;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Owner budget safety cannot be rolled back into replayable spend authority.");
}
