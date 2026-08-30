using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class PerformanceEvidenceFacts
{
    private static void GuardPerformanceEvidenceRollback(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            DO $performance_evidence_rollback_guard$
            BEGIN
                IF EXISTS (SELECT 1 FROM commercial.performance_evidence_sets) THEN
                    RAISE EXCEPTION
                        'performance evidence migration cannot roll back while evidence exists';
                END IF;
            END;
            $performance_evidence_rollback_guard$;
            """);

    private static void DropPerformanceEvidenceBoundary(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            DROP TRIGGER IF EXISTS apply_performance_evidence_task
                ON commercial.performance_evidence_sets;
            DROP FUNCTION IF EXISTS commercial.apply_performance_evidence_task();
            DROP TRIGGER IF EXISTS protect_performance_metric
                ON commercial.performance_metrics;
            DROP FUNCTION IF EXISTS commercial.enforce_performance_metric();
            DROP TRIGGER IF EXISTS protect_performance_evidence
                ON commercial.performance_evidence_sets;
            DROP FUNCTION IF EXISTS commercial.enforce_performance_evidence();
            DROP FUNCTION IF EXISTS commercial.performance_evidence_source(uuid, uuid);
            DELETE FROM commercial.human_tasks
            WHERE task_type_code = 'PERFORMANCE_FACT_REVIEW'
              AND resource_type_code = 'performance_evidence';
            DROP TABLE commercial.performance_metrics;
            DROP TABLE commercial.performance_evidence_sets;
            """);
}
