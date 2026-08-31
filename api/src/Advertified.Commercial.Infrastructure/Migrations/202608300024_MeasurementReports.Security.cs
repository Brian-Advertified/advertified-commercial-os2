using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class MeasurementReports
{
    private static void CreateMeasurementReportSecurityBoundary(
        MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
            """
            ALTER TABLE commercial.measurement_report_versions ENABLE ROW LEVEL SECURITY;
            ALTER TABLE commercial.measurement_report_versions FORCE ROW LEVEL SECURITY;
            CREATE POLICY measurement_report_tenant_scope
                ON commercial.measurement_report_versions
                USING (tenant_id = commercial.current_tenant_id())
                WITH CHECK (tenant_id = commercial.current_tenant_id());

            CREATE FUNCTION commercial.measurement_report_source(
                requested_campaign_id uuid, requested_approver_user_id uuid)
            RETURNS TABLE (
                tenant_id uuid, campaign_id uuid, opportunity_id uuid,
                campaign_version bigint, measurement_plan_json jsonb,
                approver_user_id uuid)
            LANGUAGE sql STABLE SECURITY DEFINER
            SET search_path = pg_catalog, commercial
            AS $measurement_report_source$
                SELECT campaign.tenant_id, campaign.id, brief.opportunity_id,
                    campaign.version, campaign.measurement_plan_json, reviewer.user_id
                FROM commercial.campaigns campaign
                JOIN commercial.campaign_briefs brief
                  ON brief.tenant_id = campaign.tenant_id AND brief.id = campaign.brief_id
                JOIN commercial.memberships generator
                  ON generator.tenant_id = campaign.tenant_id
                 AND generator.user_id = commercial.current_user_id()
                 AND generator.status_code = 'ACTIVE'
                 AND generator.role_code IN (
                    'platform_admin', 'internal_planner', 'agency_admin',
                    'agency_campaign_user')
                JOIN commercial.memberships reviewer
                  ON reviewer.tenant_id = campaign.tenant_id
                 AND reviewer.user_id = requested_approver_user_id
                 AND reviewer.status_code = 'ACTIVE'
                 AND reviewer.role_code IN (
                    'platform_admin', 'internal_planner',
                    'advertiser_admin', 'advertiser_approver')
                WHERE campaign.id = requested_campaign_id
                  AND campaign.tenant_id = commercial.current_tenant_id()
                  AND campaign.status_code = 'COMPLETED'
                  AND jsonb_typeof(campaign.measurement_plan_json) = 'array'
                  AND jsonb_array_length(campaign.measurement_plan_json) > 0
                  AND NOT EXISTS (
                      SELECT 1 FROM jsonb_array_elements(campaign.measurement_plan_json) item
                      WHERE jsonb_typeof(item) <> 'string' OR btrim(item #>> '{}') = '')
                  AND requested_approver_user_id <> commercial.current_user_id()
                  AND EXISTS (SELECT 1 FROM commercial.delivery_proofs proof
                      WHERE proof.buyer_tenant_id = campaign.tenant_id
                        AND proof.campaign_id = campaign.id
                        AND proof.status_code = 'APPROVED')
                  AND EXISTS (SELECT 1 FROM commercial.performance_evidence_sets evidence
                      JOIN commercial.performance_metrics metric
                        ON metric.tenant_id = evidence.tenant_id
                       AND metric.evidence_set_id = evidence.id
                      WHERE evidence.tenant_id = campaign.tenant_id
                        AND evidence.campaign_id = campaign.id
                        AND evidence.status_code = 'APPROVED')
                  AND NOT EXISTS (SELECT 1 FROM commercial.measurement_report_versions report
                      WHERE report.tenant_id = campaign.tenant_id
                        AND report.campaign_id = campaign.id
                        AND report.status_code = 'REVIEW_REQUIRED');
            $measurement_report_source$;

            REVOKE ALL ON FUNCTION commercial.measurement_report_source(uuid, uuid)
                FROM PUBLIC;
            GRANT EXECUTE ON FUNCTION commercial.measurement_report_source(uuid, uuid)
                TO advertified_app;

            CREATE FUNCTION commercial.enforce_measurement_report()
            RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER
            SET search_path = pg_catalog, commercial
            AS $measurement_report$
            DECLARE expected record;
            DECLARE expected_evidence integer;
            DECLARE expected_metrics integer;
            BEGIN
                IF TG_OP = 'DELETE' THEN
                    RAISE EXCEPTION 'measurement reports cannot be deleted';
                END IF;
                IF TG_OP = 'INSERT' THEN
                    SELECT campaign.version AS campaign_version,
                        campaign.measurement_plan_json, brief.opportunity_id
                    INTO expected
                    FROM commercial.campaigns campaign
                    JOIN commercial.campaign_briefs brief
                      ON brief.tenant_id = campaign.tenant_id AND brief.id = campaign.brief_id
                    WHERE campaign.tenant_id = NEW.tenant_id
                      AND campaign.id = NEW.campaign_id
                      AND campaign.status_code = 'COMPLETED';
                    SELECT count(DISTINCT evidence.id)::integer, count(metric.id)::integer
                    INTO expected_evidence, expected_metrics
                    FROM commercial.performance_evidence_sets evidence
                    LEFT JOIN commercial.performance_metrics metric
                      ON metric.tenant_id = evidence.tenant_id
                     AND metric.evidence_set_id = evidence.id
                    WHERE evidence.tenant_id = NEW.tenant_id
                      AND evidence.campaign_id = NEW.campaign_id
                      AND evidence.status_code = 'APPROVED';
                    IF NOT FOUND OR NEW.tenant_id <> commercial.current_tenant_id()
                       OR NEW.generated_by <> commercial.current_user_id()
                       OR NEW.approver_user_id = NEW.generated_by
                       OR NEW.status_code <> 'REVIEW_REQUIRED' OR NEW.version <> 1
                       OR NEW.generated_at_utc <> NEW.updated_at_utc
                       OR NEW.campaign_version <> expected.campaign_version
                       OR NEW.measurement_plan_json <> expected.measurement_plan_json
                       OR expected_evidence = 0 OR expected_metrics = 0
                       OR jsonb_array_length(NEW.evidence_versions_json) <> expected_evidence
                       OR cardinality(NEW.metric_ids) <> expected_metrics
                       OR EXISTS (
                           SELECT 1 FROM commercial.performance_evidence_sets evidence
                           WHERE evidence.tenant_id = NEW.tenant_id
                             AND evidence.campaign_id = NEW.campaign_id
                             AND evidence.status_code = 'APPROVED'
                             AND NOT EXISTS (
                                SELECT 1 FROM jsonb_array_elements(
                                    NEW.evidence_versions_json) item
                                WHERE (item->>'id')::uuid = evidence.id
                                  AND (item->>'version')::bigint = evidence.version))
                       OR EXISTS (
                           SELECT 1 FROM unnest(NEW.metric_ids) supplied(id)
                           WHERE NOT EXISTS (
                                SELECT 1 FROM commercial.performance_metrics metric
                                JOIN commercial.performance_evidence_sets evidence
                                  ON evidence.tenant_id = metric.tenant_id
                                 AND evidence.id = metric.evidence_set_id
                                WHERE metric.tenant_id = NEW.tenant_id
                                  AND metric.id = supplied.id
                                  AND evidence.campaign_id = NEW.campaign_id
                                  AND evidence.status_code = 'APPROVED'))
                       OR EXISTS (
                           SELECT 1 FROM commercial.performance_metrics metric
                           JOIN commercial.performance_evidence_sets evidence
                             ON evidence.tenant_id = metric.tenant_id
                            AND evidence.id = metric.evidence_set_id
                           WHERE metric.tenant_id = NEW.tenant_id
                             AND evidence.campaign_id = NEW.campaign_id
                             AND evidence.status_code = 'APPROVED'
                             AND NOT metric.id = ANY(NEW.metric_ids))
                       OR jsonb_typeof(NEW.interpretation_json) <> 'object'
                       OR NEW.interpretation_json->>'causalityStatus' <> 'NOT_ESTABLISHED'
                       OR NEW.interpretation_json->'limitations' <> NEW.limitations_json
                       OR jsonb_array_length(NEW.limitations_json) <> (
                           SELECT count(DISTINCT limitation #>> '{}')::integer
                           FROM commercial.performance_evidence_sets evidence
                           CROSS JOIN jsonb_array_elements(
                                evidence.limitations_json) limitation
                           WHERE evidence.tenant_id = NEW.tenant_id
                             AND evidence.campaign_id = NEW.campaign_id
                             AND evidence.status_code = 'APPROVED')
                       OR EXISTS (
                           SELECT 1 FROM commercial.performance_evidence_sets evidence
                           CROSS JOIN jsonb_array_elements(
                                evidence.limitations_json) limitation
                           WHERE evidence.tenant_id = NEW.tenant_id
                             AND evidence.campaign_id = NEW.campaign_id
                             AND evidence.status_code = 'APPROVED'
                             AND NOT NEW.limitations_json @> jsonb_build_array(limitation))
                       OR NOT EXISTS (SELECT 1 FROM commercial.agent_runs run
                           JOIN commercial.agent_run_steps step
                             ON step.tenant_id = run.tenant_id AND step.run_id = run.id
                           JOIN commercial.ai_usage_ledger usage
                             ON usage.tenant_id = step.tenant_id AND usage.step_id = step.id
                           WHERE run.tenant_id = NEW.tenant_id AND run.id = NEW.agent_run_id
                             AND run.campaign_id = NEW.campaign_id
                             AND run.opportunity_id IS NOT DISTINCT FROM expected.opportunity_id
                             AND run.run_kind_code = 'MEASUREMENT'
                             AND run.status_code = 'COMPLETED'
                             AND run.input_version = NEW.campaign_version
                             AND run.requested_by = NEW.generated_by
                             AND run.approver_user_id = NEW.approver_user_id
                             AND step.step_code = 'MEASUREMENT'
                             AND step.agent_code = 'measurement'
                             AND step.status_code = 'COMPLETED'
                             AND step.input_hash = NEW.input_hash
                             AND step.output_json->'interpretation' = NEW.interpretation_json
                             AND usage.provider_code = NEW.provider_code
                             AND usage.model_code = NEW.model_code
                             AND usage.units = 0
                             AND usage.tool_calls = NEW.tool_calls
                             AND usage.incremental_cost_minor = NEW.incremental_cost_minor
                             AND usage.cache_status_code = 'FIXTURE')
                       OR (SELECT count(*) FROM commercial.agent_run_steps step
                           WHERE step.tenant_id = NEW.tenant_id
                             AND step.run_id = NEW.agent_run_id) <> 1
                       OR (SELECT count(*) FROM commercial.ai_usage_ledger usage
                           WHERE usage.tenant_id = NEW.tenant_id
                             AND usage.run_id = NEW.agent_run_id) <> 1
                       OR NOT EXISTS (SELECT 1 FROM commercial.memberships membership
                           WHERE membership.tenant_id = NEW.tenant_id
                             AND membership.user_id = NEW.generated_by
                             AND membership.status_code = 'ACTIVE'
                             AND membership.role_code IN ('platform_admin', 'internal_planner',
                                'agency_admin', 'agency_campaign_user'))
                       OR NOT EXISTS (SELECT 1 FROM commercial.memberships membership
                           WHERE membership.tenant_id = NEW.tenant_id
                             AND membership.user_id = NEW.approver_user_id
                             AND membership.status_code = 'ACTIVE'
                             AND membership.role_code IN ('platform_admin', 'internal_planner',
                                'advertiser_admin', 'advertiser_approver')) THEN
                        RAISE EXCEPTION 'measurement report source is invalid';
                    END IF;
                    RETURN NEW;
                END IF;

                IF (NEW.id, NEW.tenant_id, NEW.campaign_id, NEW.version_no,
                    NEW.agent_run_id, NEW.campaign_version, NEW.measurement_plan_json,
                    NEW.evidence_versions_json, NEW.metric_ids, NEW.interpretation_json,
                    NEW.limitations_json, NEW.input_hash, NEW.output_hash,
                    NEW.agent_contract_version, NEW.prompt_version, NEW.provider_code,
                    NEW.model_code, NEW.tool_calls, NEW.incremental_cost_minor,
                    NEW.output_validated, NEW.status_collection_code,
                    NEW.approver_user_id, NEW.generated_by, NEW.generated_at_utc)
                   IS DISTINCT FROM
                   (OLD.id, OLD.tenant_id, OLD.campaign_id, OLD.version_no,
                    OLD.agent_run_id, OLD.campaign_version, OLD.measurement_plan_json,
                    OLD.evidence_versions_json, OLD.metric_ids, OLD.interpretation_json,
                    OLD.limitations_json, OLD.input_hash, OLD.output_hash,
                    OLD.agent_contract_version, OLD.prompt_version, OLD.provider_code,
                    OLD.model_code, OLD.tool_calls, OLD.incremental_cost_minor,
                    OLD.output_validated, OLD.status_collection_code,
                    OLD.approver_user_id, OLD.generated_by, OLD.generated_at_utc)
                   OR OLD.status_code <> 'REVIEW_REQUIRED'
                   OR NEW.status_code NOT IN ('APPROVED', 'REJECTED')
                   OR NEW.tenant_id <> commercial.current_tenant_id()
                   OR NEW.reviewed_by <> commercial.current_user_id()
                   OR NEW.reviewed_by <> OLD.approver_user_id
                   OR NEW.reviewed_by = OLD.generated_by
                   OR NEW.reviewed_at_utc < OLD.generated_at_utc
                   OR NEW.updated_at_utc <> NEW.reviewed_at_utc
                   OR NEW.version <> OLD.version + 1
                   OR NOT EXISTS (SELECT 1 FROM commercial.memberships membership
                       WHERE membership.tenant_id = OLD.tenant_id
                         AND membership.user_id = NEW.reviewed_by
                         AND membership.status_code = 'ACTIVE'
                         AND membership.role_code IN ('platform_admin', 'internal_planner',
                            'advertiser_admin', 'advertiser_approver')) THEN
                    RAISE EXCEPTION 'measurement report review is invalid';
                END IF;
                RETURN NEW;
            END;
            $measurement_report$;

            CREATE TRIGGER protect_measurement_report
                BEFORE INSERT OR UPDATE OR DELETE ON commercial.measurement_report_versions
                FOR EACH ROW EXECUTE FUNCTION commercial.enforce_measurement_report();
            REVOKE ALL ON FUNCTION commercial.enforce_measurement_report() FROM PUBLIC;
            GRANT SELECT, INSERT, UPDATE ON commercial.measurement_report_versions
                TO advertified_app;
            """);
}
