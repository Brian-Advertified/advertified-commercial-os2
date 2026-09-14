using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609140004_DeliveryProofRequestAccess")]
public sealed class DeliveryProofRequestAccess : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            ALTER TABLE commercial.delivery_proof_requests ENABLE ROW LEVEL SECURITY;
            ALTER TABLE commercial.delivery_proof_requests FORCE ROW LEVEL SECURITY;

            CREATE POLICY delivery_proof_request_participant_select
                ON commercial.delivery_proof_requests FOR SELECT
                USING (
                    buyer_tenant_id = commercial.current_tenant_id()
                    OR supplier_tenant_id = commercial.current_tenant_id());

            CREATE POLICY delivery_proof_request_buyer_insert
                ON commercial.delivery_proof_requests FOR INSERT
                WITH CHECK (buyer_tenant_id = commercial.current_tenant_id());

            GRANT SELECT, INSERT ON TABLE commercial.delivery_proof_requests TO advertified_app;
            """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Delivery proof request access is forward-only.");
}
