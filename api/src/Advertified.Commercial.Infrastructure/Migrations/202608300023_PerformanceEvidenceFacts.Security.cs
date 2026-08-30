using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class PerformanceEvidenceFacts
{
    private static void CreatePerformanceEvidenceSecurityBoundary(
        MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
            """
            ALTER TABLE commercial.performance_evidence_sets ENABLE ROW LEVEL SECURITY;
            ALTER TABLE commercial.performance_evidence_sets FORCE ROW LEVEL SECURITY;
            CREATE POLICY performance_evidence_tenant_scope
                ON commercial.performance_evidence_sets
                USING (tenant_id = commercial.current_tenant_id())
                WITH CHECK (tenant_id = commercial.current_tenant_id());

            ALTER TABLE commercial.performance_metrics ENABLE ROW LEVEL SECURITY;
            ALTER TABLE commercial.performance_metrics FORCE ROW LEVEL SECURITY;
            CREATE POLICY performance_metric_tenant_scope ON commercial.performance_metrics
                USING (tenant_id = commercial.current_tenant_id())
                WITH CHECK (tenant_id = commercial.current_tenant_id());

            CREATE FUNCTION commercial.performance_evidence_source(
                requested_campaign_id uuid, requested_reviewer_user_id uuid)
            RETURNS TABLE (
                tenant_id uuid, campaign_id uuid,
                campaign_start date, campaign_end date,
                completed_at_utc timestamptz, reviewer_user_id uuid)
            LANGUAGE sql STABLE SECURITY DEFINER
            SET search_path = pg_catalog, commercial
            AS $performance_evidence_source$
                SELECT campaign.tenant_id, campaign.id,
                    campaign.start_date, campaign.end_date,
                    campaign.completed_at_utc, reviewer.user_id
                FROM commercial.campaigns campaign
                JOIN commercial.memberships submitter
                  ON submitter.tenant_id = campaign.tenant_id
                 AND submitter.user_id = commercial.current_user_id()
                 AND submitter.status_code = 'ACTIVE'
                 AND submitter.role_code IN (
                    'platform_admin', 'internal_planner', 'agency_admin',
                    'agency_campaign_user')
                JOIN commercial.memberships reviewer
                  ON reviewer.tenant_id = campaign.tenant_id
                 AND reviewer.user_id = requested_reviewer_user_id
                 AND reviewer.status_code = 'ACTIVE'
                 AND reviewer.role_code IN (
                    'platform_admin', 'internal_planner',
                    'advertiser_admin', 'advertiser_approver')
                WHERE campaign.id = requested_campaign_id
                  AND campaign.tenant_id = commercial.current_tenant_id()
                  AND campaign.status_code = 'COMPLETED'
                  AND campaign.completed_at_utc IS NOT NULL
                  AND requested_reviewer_user_id <> commercial.current_user_id()
                  AND EXISTS (
                      SELECT 1 FROM commercial.delivery_proofs proof
                      WHERE proof.buyer_tenant_id = campaign.tenant_id
                        AND proof.campaign_id = campaign.id
                        AND proof.status_code = 'APPROVED');
            $performance_evidence_source$;

            REVOKE ALL ON FUNCTION commercial.performance_evidence_source(uuid, uuid)
                FROM PUBLIC;
            GRANT EXECUTE ON FUNCTION commercial.performance_evidence_source(uuid, uuid)
                TO advertified_app;

            CREATE FUNCTION commercial.enforce_performance_evidence()
            RETURNS trigger LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, commercial
            AS $performance_evidence$
            DECLARE campaign_row record;
            BEGIN
                IF TG_OP = 'DELETE' THEN
                    RAISE EXCEPTION 'performance evidence cannot be deleted';
                END IF;
                IF TG_OP = 'INSERT' THEN
                    SELECT campaign.status_code, campaign.completed_at_utc
                    INTO campaign_row
                    FROM commercial.campaigns campaign
                    WHERE campaign.tenant_id = NEW.tenant_id
                      AND campaign.id = NEW.campaign_id;
                    IF NOT FOUND OR campaign_row.status_code <> 'COMPLETED'
                       OR NEW.tenant_id <> commercial.current_tenant_id()
                       OR NEW.created_by <> commercial.current_user_id()
                       OR NEW.reviewer_user_id = NEW.created_by
                       OR NEW.status_code <> 'DRAFT' OR NEW.version <> 0
                       OR NEW.updated_at_utc <> NEW.created_at_utc
                       OR NEW.captured_at_utc < campaign_row.completed_at_utc
                       OR NEW.captured_at_utc > NEW.created_at_utc
                       OR NEW.protected_object_key <>
                            'protected/' || replace(NEW.tenant_id::text, '-', '') ||
                            '/campaigns/' || replace(NEW.campaign_id::text, '-', '') ||
                            '/performance/' || replace(NEW.id::text, '-', '') || '/' ||
                            NEW.content_sha256
                       OR jsonb_array_length(NEW.limitations_json) > 20
                       OR EXISTS (
                           SELECT 1 FROM jsonb_array_elements(NEW.limitations_json) item
                           WHERE jsonb_typeof(item) <> 'string'
                              OR btrim(item #>> '{}') = ''
                              OR length(item #>> '{}') > 500)
                       OR NOT EXISTS (
                           SELECT 1 FROM commercial.memberships membership
                           WHERE membership.tenant_id = NEW.tenant_id
                             AND membership.user_id = NEW.created_by
                             AND membership.status_code = 'ACTIVE'
                             AND membership.role_code IN (
                                'platform_admin', 'internal_planner',
                                'agency_admin', 'agency_campaign_user'))
                       OR NOT EXISTS (
                           SELECT 1 FROM commercial.memberships membership
                           WHERE membership.tenant_id = NEW.tenant_id
                             AND membership.user_id = NEW.reviewer_user_id
                             AND membership.status_code = 'ACTIVE'
                             AND membership.role_code IN (
                                'platform_admin', 'internal_planner',
                                'advertiser_admin', 'advertiser_approver'))
                       OR NOT EXISTS (
                           SELECT 1 FROM commercial.delivery_proofs proof
                           WHERE proof.buyer_tenant_id = NEW.tenant_id
                             AND proof.campaign_id = NEW.campaign_id
                             AND proof.status_code = 'APPROVED') THEN
                        RAISE EXCEPTION 'performance evidence source is invalid';
                    END IF;
                    RETURN NEW;
                END IF;

                IF (NEW.id, NEW.tenant_id, NEW.campaign_id, NEW.source_reference,
                    NEW.file_name, NEW.media_type, NEW.size_bytes, NEW.content_sha256,
                    NEW.signature_validated, NEW.malware_scan_status_collection_code,
                    NEW.malware_scan_status_code, NEW.protected_object_key,
                    NEW.captured_at_utc, NEW.methodology, NEW.limitations_json,
                    NEW.quality_status_collection_code, NEW.quality_status_code,
                    NEW.status_collection_code, NEW.reviewer_user_id,
                    NEW.created_by, NEW.created_at_utc) IS DISTINCT FROM
                   (OLD.id, OLD.tenant_id, OLD.campaign_id, OLD.source_reference,
                    OLD.file_name, OLD.media_type, OLD.size_bytes, OLD.content_sha256,
                    OLD.signature_validated, OLD.malware_scan_status_collection_code,
                    OLD.malware_scan_status_code, OLD.protected_object_key,
                    OLD.captured_at_utc, OLD.methodology, OLD.limitations_json,
                    OLD.quality_status_collection_code, OLD.quality_status_code,
                    OLD.status_collection_code, OLD.reviewer_user_id,
                    OLD.created_by, OLD.created_at_utc) THEN
                    RAISE EXCEPTION 'performance evidence source is immutable';
                END IF;

                IF OLD.status_code = 'DRAFT' AND NEW.status_code = 'SUBMITTED' THEN
                    IF NEW.tenant_id <> commercial.current_tenant_id()
                       OR NEW.submitted_by <> commercial.current_user_id()
                       OR NEW.submitted_by <> OLD.created_by
                       OR NEW.submitted_at_utc < OLD.created_at_utc
                       OR NEW.updated_at_utc <> NEW.submitted_at_utc
                       OR NEW.version <> 1
                       OR NOT EXISTS (
                           SELECT 1 FROM commercial.performance_metrics metric
                           WHERE metric.tenant_id = OLD.tenant_id
                             AND metric.evidence_set_id = OLD.id) THEN
                        RAISE EXCEPTION 'performance evidence facts are incomplete';
                    END IF;
                    RETURN NEW;
                END IF;

                IF OLD.status_code = 'SUBMITTED'
                   AND NEW.status_code IN ('APPROVED', 'REJECTED') THEN
                    IF NEW.tenant_id <> commercial.current_tenant_id()
                       OR NEW.reviewed_by <> commercial.current_user_id()
                       OR NEW.reviewed_by <> OLD.reviewer_user_id
                       OR NEW.reviewed_by = OLD.submitted_by
                       OR NEW.reviewed_at_utc < OLD.submitted_at_utc
                       OR NEW.updated_at_utc <> NEW.reviewed_at_utc
                       OR NEW.version <> OLD.version + 1
                       OR (NEW.submitted_by, NEW.submitted_at_utc) IS DISTINCT FROM
                          (OLD.submitted_by, OLD.submitted_at_utc)
                       OR (NEW.status_code = 'APPROVED'
                           AND OLD.quality_status_code = 'UNUSABLE')
                       OR NOT EXISTS (
                           SELECT 1 FROM commercial.memberships membership
                           WHERE membership.tenant_id = OLD.tenant_id
                             AND membership.user_id = NEW.reviewed_by
                             AND membership.status_code = 'ACTIVE'
                             AND membership.role_code IN (
                                'platform_admin', 'internal_planner',
                                'advertiser_admin', 'advertiser_approver')) THEN
                        RAISE EXCEPTION 'performance evidence review is invalid';
                    END IF;
                    RETURN NEW;
                END IF;
                RAISE EXCEPTION 'performance evidence transition is invalid';
            END;
            $performance_evidence$;

            CREATE TRIGGER protect_performance_evidence
                BEFORE INSERT OR UPDATE OR DELETE ON commercial.performance_evidence_sets
                FOR EACH ROW EXECUTE FUNCTION commercial.enforce_performance_evidence();

            CREATE FUNCTION commercial.enforce_performance_metric()
            RETURNS trigger LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, commercial
            AS $performance_metric$
            DECLARE expected record;
            BEGIN
                IF TG_OP <> 'INSERT' THEN
                    RAISE EXCEPTION 'performance metrics are immutable';
                END IF;
                SELECT evidence.status_code, evidence.created_by,
                    campaign.start_date, campaign.end_date
                INTO expected
                FROM commercial.performance_evidence_sets evidence
                JOIN commercial.campaigns campaign
                  ON campaign.tenant_id = evidence.tenant_id
                 AND campaign.id = evidence.campaign_id
                WHERE evidence.tenant_id = NEW.tenant_id
                  AND evidence.id = NEW.evidence_set_id
                  AND evidence.campaign_id = NEW.campaign_id;
                IF NOT FOUND OR expected.status_code <> 'DRAFT'
                   OR NEW.tenant_id <> commercial.current_tenant_id()
                   OR NEW.created_by <> commercial.current_user_id()
                   OR NEW.created_by <> expected.created_by
                   OR NEW.period_start < expected.start_date
                   OR NEW.period_end > expected.end_date THEN
                    RAISE EXCEPTION 'performance metric source is invalid';
                END IF;
                RETURN NEW;
            END;
            $performance_metric$;

            CREATE TRIGGER protect_performance_metric
                BEFORE INSERT OR UPDATE OR DELETE ON commercial.performance_metrics
                FOR EACH ROW EXECUTE FUNCTION commercial.enforce_performance_metric();

            REVOKE ALL ON FUNCTION commercial.enforce_performance_evidence() FROM PUBLIC;
            REVOKE ALL ON FUNCTION commercial.enforce_performance_metric() FROM PUBLIC;
            GRANT SELECT, INSERT, UPDATE ON commercial.performance_evidence_sets
                TO advertified_app;
            GRANT SELECT, INSERT ON commercial.performance_metrics TO advertified_app;
            """);
}
