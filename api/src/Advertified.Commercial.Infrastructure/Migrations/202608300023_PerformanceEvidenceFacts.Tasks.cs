using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class PerformanceEvidenceFacts
{
    private static void CreatePerformanceEvidenceTaskBoundary(
        MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
            """
            CREATE FUNCTION commercial.apply_performance_evidence_task()
            RETURNS trigger LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, commercial
            AS $performance_evidence_task$
            DECLARE brief_opportunity uuid;
            BEGIN
                IF OLD.status_code = 'DRAFT' AND NEW.status_code = 'SUBMITTED' THEN
                    SELECT brief.opportunity_id INTO brief_opportunity
                    FROM commercial.campaigns campaign
                    JOIN commercial.campaign_briefs brief
                      ON brief.tenant_id = campaign.tenant_id
                     AND brief.id = campaign.brief_id
                    WHERE campaign.tenant_id = NEW.tenant_id
                      AND campaign.id = NEW.campaign_id;
                    INSERT INTO commercial.human_tasks (
                        id, tenant_id, opportunity_id, task_type_code, status_code,
                        title, why_it_matters, resource_type_code, resource_id,
                        resource_version, assignee_user_id, action_schema_json,
                        version, created_at_utc)
                    VALUES (gen_random_uuid(), NEW.tenant_id, brief_opportunity,
                        'PERFORMANCE_FACT_REVIEW', 'PENDING',
                        'Review sourced performance facts',
                        'Confirm the source, method, quality and limitations before interpretation.',
                        'performance_evidence', NEW.id, NEW.version,
                        NEW.reviewer_user_id, '{}'::jsonb, 1, NEW.submitted_at_utc);
                    RETURN NEW;
                END IF;
                IF OLD.status_code = 'SUBMITTED'
                   AND NEW.status_code IN ('APPROVED', 'REJECTED') THEN
                    UPDATE commercial.human_tasks
                    SET status_code = 'COMPLETED', completed_by = NEW.reviewed_by,
                        completed_at_utc = NEW.reviewed_at_utc,
                        completion_json = jsonb_build_object(
                            'decision', NEW.status_code,
                            'evidenceVersion', NEW.version),
                        resource_version = NEW.version, version = version + 1
                    WHERE tenant_id = NEW.tenant_id
                      AND task_type_code = 'PERFORMANCE_FACT_REVIEW'
                      AND resource_type_code = 'performance_evidence'
                      AND resource_id = NEW.id AND status_code = 'PENDING';
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'performance evidence review task is missing';
                    END IF;
                    RETURN NEW;
                END IF;
                RETURN NEW;
            END;
            $performance_evidence_task$;

            CREATE TRIGGER apply_performance_evidence_task
                AFTER UPDATE ON commercial.performance_evidence_sets
                FOR EACH ROW EXECUTE FUNCTION commercial.apply_performance_evidence_task();

            REVOKE ALL ON FUNCTION commercial.apply_performance_evidence_task() FROM PUBLIC;
            """);
}
