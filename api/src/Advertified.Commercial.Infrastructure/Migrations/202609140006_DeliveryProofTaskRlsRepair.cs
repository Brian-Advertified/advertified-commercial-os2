using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609140006_DeliveryProofTaskRlsRepair")]
public sealed class DeliveryProofTaskRlsRepair : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RepairSql);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Delivery proof task RLS repair is forward-only.");

    private const string RepairSql = """
        ALTER TABLE commercial.delivery_proof_requests
            ADD COLUMN campaign_owner_user_id uuid NULL;
        ALTER TABLE commercial.delivery_proof_requests
            ADD CONSTRAINT fk_delivery_proof_request_campaign_owner
            FOREIGN KEY (campaign_owner_user_id) REFERENCES commercial.users(id);

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
                      AND proof.submitted_by = commercial.current_user_id()
                      AND proof.submitter_tenant_id = commercial.current_tenant_id()
                      AND proof.status_code = 'SUBMITTED'
                      AND proof.version = commercial.human_tasks.resource_version
                )
            );
        """;
}
