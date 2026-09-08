using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609080011_AiMonthlyBudget")]
public sealed class AiMonthlyBudget : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
        CREATE TABLE governance.ai_monthly_budget_ledger (
            month_start_utc date NOT NULL,
            run_id uuid NOT NULL,
            step_id uuid NOT NULL,
            tenant_id uuid NOT NULL,
            maximum_cost_usd_micros bigint NOT NULL,
            actual_cost_usd_micros bigint,
            status_code character varying(20) NOT NULL,
            created_at_utc timestamp with time zone NOT NULL,
            completed_at_utc timestamp with time zone,
            CONSTRAINT pk_ai_monthly_budget_ledger
                PRIMARY KEY (month_start_utc, run_id, step_id),
            CONSTRAINT ck_ai_monthly_budget_cost CHECK (
                maximum_cost_usd_micros > 0 AND
                maximum_cost_usd_micros <= 5000000 AND
                (actual_cost_usd_micros IS NULL OR
                 actual_cost_usd_micros BETWEEN 0 AND maximum_cost_usd_micros)),
            CONSTRAINT ck_ai_monthly_budget_status CHECK (
                status_code IN ('RESERVED', 'COMPLETED'))
        );

        CREATE FUNCTION governance.read_ai_monthly_budget(p_month_start date)
        RETURNS bigint LANGUAGE sql STABLE SECURITY DEFINER
        SET search_path = pg_catalog AS $function$
            SELECT COALESCE(sum(COALESCE(
                actual_cost_usd_micros, maximum_cost_usd_micros)), 0)::bigint
            FROM governance.ai_monthly_budget_ledger
            WHERE month_start_utc = p_month_start
        $function$;

        CREATE FUNCTION governance.reserve_ai_monthly_budget(
            p_month_start date, p_run_id uuid, p_step_id uuid,
            p_tenant_id uuid, p_requested bigint)
        RETURNS boolean LANGUAGE plpgsql VOLATILE SECURITY DEFINER
        SET search_path = pg_catalog AS $function$
        DECLARE reserved bigint;
        BEGIN
            IF p_requested <= 0 OR p_requested > 5000000 THEN RETURN false; END IF;
            PERFORM pg_advisory_xact_lock(hashtextextended(
                'advertified:ai:' || p_month_start::text, 0));
            IF EXISTS (SELECT 1 FROM governance.ai_monthly_budget_ledger
                WHERE month_start_utc = p_month_start
                  AND run_id = p_run_id AND step_id = p_step_id) THEN
                RETURN EXISTS (SELECT 1 FROM governance.ai_monthly_budget_ledger
                    WHERE month_start_utc = p_month_start
                      AND run_id = p_run_id AND step_id = p_step_id
                      AND tenant_id = p_tenant_id
                      AND maximum_cost_usd_micros = p_requested);
            END IF;
            SELECT governance.read_ai_monthly_budget(p_month_start) INTO reserved;
            IF p_requested > 5000000 - reserved THEN RETURN false; END IF;
            INSERT INTO governance.ai_monthly_budget_ledger VALUES (
                p_month_start, p_run_id, p_step_id, p_tenant_id,
                p_requested, NULL, 'RESERVED', statement_timestamp(), NULL);
            RETURN true;
        END $function$;

        CREATE FUNCTION governance.complete_ai_monthly_budget(
            p_month_start date, p_run_id uuid, p_step_id uuid, p_actual bigint)
        RETURNS boolean LANGUAGE plpgsql VOLATILE SECURITY DEFINER
        SET search_path = pg_catalog AS $function$
        BEGIN
            UPDATE governance.ai_monthly_budget_ledger
            SET actual_cost_usd_micros = p_actual,
                status_code = 'COMPLETED', completed_at_utc = statement_timestamp()
            WHERE month_start_utc = p_month_start AND run_id = p_run_id
              AND step_id = p_step_id AND p_actual BETWEEN 0 AND maximum_cost_usd_micros;
            RETURN FOUND;
        END $function$;

        REVOKE ALL ON TABLE governance.ai_monthly_budget_ledger FROM PUBLIC;
        REVOKE ALL ON FUNCTION governance.read_ai_monthly_budget(date) FROM PUBLIC;
        REVOKE ALL ON FUNCTION governance.reserve_ai_monthly_budget(
            date, uuid, uuid, uuid, bigint) FROM PUBLIC;
        REVOKE ALL ON FUNCTION governance.complete_ai_monthly_budget(
            date, uuid, uuid, bigint) FROM PUBLIC;
        GRANT EXECUTE ON FUNCTION governance.read_ai_monthly_budget(date) TO advertified_app;
        GRANT EXECUTE ON FUNCTION governance.reserve_ai_monthly_budget(
            date, uuid, uuid, uuid, bigint) TO advertified_app;
        GRANT EXECUTE ON FUNCTION governance.complete_ai_monthly_budget(
            date, uuid, uuid, bigint) TO advertified_app;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
        DROP FUNCTION governance.complete_ai_monthly_budget(date, uuid, uuid, bigint);
        DROP FUNCTION governance.reserve_ai_monthly_budget(date, uuid, uuid, uuid, bigint);
        DROP FUNCTION governance.read_ai_monthly_budget(date);
        DROP TABLE governance.ai_monthly_budget_ledger;
        """);
}
