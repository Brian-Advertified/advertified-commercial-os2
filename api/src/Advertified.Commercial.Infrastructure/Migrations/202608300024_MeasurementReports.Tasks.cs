using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class MeasurementReports
{
    private static void CreateMeasurementReportTaskBoundary(
        MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
            """
            CREATE FUNCTION commercial.apply_measurement_report_task()
            RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER
            SET search_path = pg_catalog, commercial
            AS $measurement_report_task$
            DECLARE brief_opportunity uuid;
            BEGIN
                IF TG_OP = 'INSERT' THEN
                    SELECT brief.opportunity_id INTO brief_opportunity
                    FROM commercial.campaigns campaign
                    JOIN commercial.campaign_briefs brief
                      ON brief.tenant_id = campaign.tenant_id AND brief.id = campaign.brief_id
                    WHERE campaign.tenant_id = NEW.tenant_id
                      AND campaign.id = NEW.campaign_id;
                    INSERT INTO commercial.human_tasks (
                        id, tenant_id, opportunity_id, task_type_code, status_code,
                        title, why_it_matters, resource_type_code, resource_id,
                        resource_version, assignee_user_id, action_schema_json,
                        version, created_at_utc)
                    VALUES (gen_random_uuid(), NEW.tenant_id, brief_opportunity,
                        'MEASUREMENT_REPORT_REVIEW', 'PENDING',
                        'Review sourced measurement report',
                        'Approve only the exact sourced interpretation for client use.',
                        'measurement_report', NEW.id, NEW.version,
                        NEW.approver_user_id, '{}'::jsonb, 1, NEW.generated_at_utc);
                    RETURN NEW;
                END IF;
                IF OLD.status_code = 'REVIEW_REQUIRED'
                   AND NEW.status_code IN ('APPROVED', 'REJECTED') THEN
                    UPDATE commercial.human_tasks
                    SET status_code = 'COMPLETED', completed_by = NEW.reviewed_by,
                        completed_at_utc = NEW.reviewed_at_utc,
                        completion_json = jsonb_build_object(
                            'decision', NEW.status_code, 'reportVersion', NEW.version),
                        resource_version = NEW.version, version = version + 1
                    WHERE tenant_id = NEW.tenant_id
                      AND task_type_code = 'MEASUREMENT_REPORT_REVIEW'
                      AND resource_type_code = 'measurement_report'
                      AND resource_id = NEW.id AND status_code = 'PENDING';
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'measurement report review task is missing';
                    END IF;
                END IF;
                RETURN NEW;
            END;
            $measurement_report_task$;

            CREATE TRIGGER apply_measurement_report_task
                AFTER INSERT OR UPDATE ON commercial.measurement_report_versions
                FOR EACH ROW EXECUTE FUNCTION commercial.apply_measurement_report_task();
            REVOKE ALL ON FUNCTION commercial.apply_measurement_report_task() FROM PUBLIC;
            """);
}
