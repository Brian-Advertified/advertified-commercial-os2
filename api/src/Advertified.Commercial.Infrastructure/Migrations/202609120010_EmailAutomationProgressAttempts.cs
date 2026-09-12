using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609120010_EmailAutomationProgressAttempts")]
public sealed class EmailAutomationProgressAttempts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE FUNCTION commercial.email_automation_attempts(
            p_tenant_id uuid,
            p_run_id uuid)
        RETURNS integer
        LANGUAGE plpgsql SECURITY DEFINER
        SET search_path TO 'pg_catalog', 'commercial'
        AS $$
        DECLARE result integer;
        BEGIN
            IF p_tenant_id IS NULL OR
                p_tenant_id IS DISTINCT FROM commercial.current_tenant_id() THEN
                RAISE EXCEPTION 'Tenant does not match the current session';
            END IF;
            SELECT COALESCE(claim.attempts, 0)::integer
              INTO result
            FROM commercial.email_proposal_automation_runs run
            LEFT JOIN commercial.email_worker_claims claim
              ON claim.tenant_id = run.tenant_id
             AND claim.inbound_email_id = run.inbound_email_id
            WHERE run.tenant_id = p_tenant_id
              AND run.id = p_run_id;
            RETURN COALESCE(result, 0);
        END;
        $$;
        REVOKE ALL ON FUNCTION commercial.email_automation_attempts(uuid, uuid)
            FROM PUBLIC;
        GRANT EXECUTE ON FUNCTION commercial.email_automation_attempts(uuid, uuid)
            TO advertified_app;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP FUNCTION IF EXISTS commercial.email_automation_attempts(uuid, uuid);
        """);
}
