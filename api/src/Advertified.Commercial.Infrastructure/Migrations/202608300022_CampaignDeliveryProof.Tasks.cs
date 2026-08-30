using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class CampaignDeliveryProof
{
    private static void CreateDeliveryProofTaskBoundary(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            CREATE FUNCTION commercial.apply_delivery_proof_task()
            RETURNS trigger LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, commercial
            AS $delivery_proof_task$
            DECLARE campaign_owner uuid;
            DECLARE brief_opportunity uuid;
            BEGIN
                IF TG_OP = 'INSERT' THEN
                    SELECT campaign.owner_user_id, brief.opportunity_id
                    INTO campaign_owner, brief_opportunity
                    FROM commercial.campaigns campaign
                    JOIN commercial.campaign_briefs brief
                      ON brief.tenant_id = campaign.tenant_id
                     AND brief.id = campaign.brief_id
                    WHERE campaign.tenant_id = NEW.buyer_tenant_id
                      AND campaign.id = NEW.campaign_id;
                    INSERT INTO commercial.human_tasks (
                        id, tenant_id, opportunity_id, task_type_code, status_code,
                        title, why_it_matters, resource_type_code, resource_id,
                        resource_version, assignee_user_id, action_schema_json,
                        version, created_at_utc)
                    VALUES (gen_random_uuid(), NEW.buyer_tenant_id, brief_opportunity,
                        'DELIVERY_PROOF_REVIEW', 'PENDING', 'Review delivery proof',
                        'Verify the exact supplier proof before it is accepted.',
                        'delivery_proof', NEW.id, NEW.version, campaign_owner,
                        '{}'::jsonb, 1, NEW.submitted_at_utc);
                    RETURN NEW;
                END IF;
                UPDATE commercial.human_tasks
                SET status_code = 'COMPLETED', completed_by = NEW.reviewed_by,
                    completed_at_utc = NEW.reviewed_at_utc,
                    completion_json = jsonb_build_object(
                        'decision', NEW.status_code, 'proofVersion', NEW.version),
                    resource_version = NEW.version, version = version + 1
                WHERE tenant_id = NEW.buyer_tenant_id
                  AND task_type_code = 'DELIVERY_PROOF_REVIEW'
                  AND resource_type_code = 'delivery_proof'
                  AND resource_id = NEW.id AND status_code = 'PENDING';
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'delivery proof review task is missing';
                END IF;
                RETURN NEW;
            END;
            $delivery_proof_task$;

            CREATE TRIGGER apply_delivery_proof_task
                AFTER INSERT OR UPDATE ON commercial.delivery_proofs
                FOR EACH ROW EXECUTE FUNCTION commercial.apply_delivery_proof_task();

            REVOKE ALL ON FUNCTION commercial.apply_delivery_proof_task() FROM PUBLIC;
            """);
}
