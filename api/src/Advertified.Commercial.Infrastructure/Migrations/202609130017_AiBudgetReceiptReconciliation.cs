using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609130017_AiBudgetReceiptReconciliation")]
public sealed class AiBudgetReceiptReconciliation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        -- Keep the owner's US$10 global ceiling and every one-shot reservation row.
        -- Once an invocation has an exact accepted provider receipt, only its reconciled
        -- actual cost remains committed. Failed/uncertain calls without an exact receipt
        -- continue to hold their full maximum reservation.
        CREATE OR REPLACE FUNCTION governance.read_ai_monthly_budget(p_month_start date)
        RETURNS bigint LANGUAGE sql STABLE SECURITY DEFINER
        SET search_path = pg_catalog AS $function$
            SELECT COALESCE(sum(
                CASE
                    WHEN status_code = 'COMPLETED' AND actual_cost_usd_micros IS NOT NULL
                        THEN actual_cost_usd_micros
                    ELSE maximum_cost_usd_micros
                END
            ), 0)::bigint
            FROM governance.ai_monthly_budget_ledger
        $function$;

        CREATE OR REPLACE FUNCTION governance.reserve_ai_monthly_budget(
            p_month_start date, p_run_id uuid, p_step_id uuid,
            p_tenant_id uuid, p_requested bigint)
        RETURNS boolean LANGUAGE plpgsql VOLATILE SECURITY DEFINER
        SET search_path = pg_catalog AS $function$
        DECLARE committed bigint;
        BEGIN
            IF p_requested IS NULL OR p_requested <= 0 OR p_requested > 10000000 THEN
                RETURN false;
            END IF;
            PERFORM pg_advisory_xact_lock(hashtextextended('advertified:ai:owner-budget', 0));
            IF EXISTS (SELECT 1 FROM governance.ai_monthly_budget_ledger
                WHERE run_id = p_run_id AND step_id = p_step_id) THEN RETURN false; END IF;
            SELECT COALESCE(sum(
                CASE
                    WHEN status_code = 'COMPLETED' AND actual_cost_usd_micros IS NOT NULL
                        THEN actual_cost_usd_micros
                    ELSE maximum_cost_usd_micros
                END
            ), 0)::bigint INTO committed
                FROM governance.ai_monthly_budget_ledger;
            IF p_requested > 10000000 - committed THEN RETURN false; END IF;
            INSERT INTO governance.ai_monthly_budget_ledger VALUES (
                p_month_start, p_run_id, p_step_id, p_tenant_id,
                p_requested, NULL, 'RESERVED', statement_timestamp(), NULL);
            RETURN true;
        END $function$;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "AI budget receipt reconciliation is safety-critical and requires a forward migration to change.");
}
