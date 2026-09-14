using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609140007_DeliveryProofTaskSourceRepair")]
public sealed class DeliveryProofTaskSourceRepair : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RepairSql);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Delivery proof task source repair is forward-only.");

    private const string RepairSql = """
        ALTER TABLE commercial.delivery_proof_requests
            ADD COLUMN opportunity_id uuid NULL;
        ALTER TABLE commercial.delivery_proof_requests
            ADD CONSTRAINT fk_delivery_proof_request_opportunity
            FOREIGN KEY (buyer_tenant_id, opportunity_id)
            REFERENCES commercial.opportunities(tenant_id, id);

        DROP POLICY human_tasks_delivery_proof_supplier_insert
            ON commercial.human_tasks;
        CREATE POLICY human_tasks_delivery_proof_supplier_insert
            ON commercial.human_tasks FOR INSERT
            WITH CHECK (
                task_type_code = 'DELIVERY_PROOF_REVIEW'
                AND status_code = 'PENDING'
                AND resource_type_code = 'delivery_proof'
                AND EXISTS (
                    SELECT 1
                    FROM commercial.delivery_proof_requests request
                    JOIN commercial.delivery_proofs proof
                      ON proof.id = commercial.human_tasks.resource_id
                     AND proof.buyer_tenant_id = request.buyer_tenant_id
                     AND proof.supplier_tenant_id = request.supplier_tenant_id
                     AND proof.campaign_id = request.campaign_id
                     AND proof.booking_id = request.booking_id
                    WHERE request.buyer_tenant_id = commercial.human_tasks.tenant_id
                      AND request.supplier_tenant_id = commercial.current_tenant_id()
                      AND request.campaign_owner_user_id = commercial.human_tasks.assignee_user_id
                      AND commercial.human_tasks.opportunity_id
                            IS NOT DISTINCT FROM request.opportunity_id
                      AND proof.submitted_by = commercial.current_user_id()
                      AND proof.submitter_tenant_id = commercial.current_tenant_id()
                      AND proof.status_code = 'SUBMITTED'
                      AND proof.version = commercial.human_tasks.resource_version
                )
            );

        CREATE OR REPLACE FUNCTION commercial.apply_delivery_proof_task() RETURNS trigger
            LANGUAGE plpgsql SECURITY DEFINER
            SET search_path TO 'pg_catalog', 'commercial'
        AS $$
        DECLARE campaign_owner uuid;
        DECLARE brief_opportunity uuid;
        BEGIN
            IF TG_OP = 'INSERT' THEN
                SELECT request.campaign_owner_user_id, request.opportunity_id
                INTO campaign_owner, brief_opportunity
                FROM commercial.delivery_proof_requests request
                WHERE request.buyer_tenant_id = NEW.buyer_tenant_id
                  AND request.supplier_tenant_id = NEW.supplier_tenant_id
                  AND request.campaign_id = NEW.campaign_id
                  AND request.booking_id = NEW.booking_id;
                IF NOT FOUND OR campaign_owner IS NULL THEN
                    RAISE EXCEPTION 'delivery proof review task source is missing';
                END IF;
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
        $$;
        REVOKE ALL ON FUNCTION commercial.apply_delivery_proof_task() FROM PUBLIC;
        """;
}
