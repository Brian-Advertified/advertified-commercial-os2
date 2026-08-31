using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class MeasurementReports
{
    private static void GuardMeasurementReportRollback(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            DO $measurement_report_rollback_guard$
            BEGIN
                IF EXISTS (SELECT 1 FROM commercial.measurement_report_versions)
                   OR EXISTS (SELECT 1 FROM commercial.agent_runs WHERE campaign_id IS NOT NULL) THEN
                    RAISE EXCEPTION
                        'measurement report migration cannot roll back while report traces exist';
                END IF;
            END;
            $measurement_report_rollback_guard$;
            """);

    private static void DropMeasurementReportBoundary(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            DROP TRIGGER IF EXISTS apply_measurement_report_task
                ON commercial.measurement_report_versions;
            DROP FUNCTION IF EXISTS commercial.apply_measurement_report_task();
            DROP TRIGGER IF EXISTS protect_measurement_report
                ON commercial.measurement_report_versions;
            DROP FUNCTION IF EXISTS commercial.enforce_measurement_report();
            DROP FUNCTION IF EXISTS commercial.measurement_report_source(uuid, uuid);
            DELETE FROM commercial.human_tasks
            WHERE task_type_code = 'MEASUREMENT_REPORT_REVIEW'
              AND resource_type_code = 'measurement_report';
            DROP TABLE commercial.measurement_report_versions;
            ALTER TABLE commercial.agent_runs DROP CONSTRAINT ck_agent_runs_work_scope;
            DROP INDEX commercial.ix_agent_runs_campaign;
            ALTER TABLE commercial.agent_runs DROP CONSTRAINT fk_agent_runs_campaign;
            ALTER TABLE commercial.agent_runs DROP COLUMN campaign_id;
            ALTER TABLE commercial.agent_runs ALTER COLUMN opportunity_id SET NOT NULL;
            """);
}
