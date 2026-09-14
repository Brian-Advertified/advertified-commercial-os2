using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609140003_DeliveryProofRequestRlsRepair")]
public sealed class DeliveryProofRequestRlsRepair : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            CREATE TABLE commercial.delivery_proof_requests (
                buyer_tenant_id uuid NOT NULL,
                supplier_tenant_id uuid NOT NULL,
                campaign_id uuid NOT NULL,
                booking_id uuid NOT NULL,
                supplier_name text NOT NULL,
                product_name text NOT NULL,
                channel_code text NOT NULL,
                geography text NOT NULL,
                flight_start date NOT NULL,
                flight_end date NOT NULL,
                proof_requested_by uuid NOT NULL,
                proof_requested_at_utc timestamp with time zone NOT NULL,
                proof_request_reason text NOT NULL,
                CONSTRAINT pk_delivery_proof_requests PRIMARY KEY (
                    buyer_tenant_id, campaign_id, booking_id),
                CONSTRAINT ck_delivery_proof_request_flight
                    CHECK (flight_end >= flight_start),
                CONSTRAINT ck_delivery_proof_request_reason
                    CHECK (btrim(proof_request_reason) <> '')
            );

            CREATE INDEX ix_delivery_proof_request_supplier
                ON commercial.delivery_proof_requests (
                    supplier_tenant_id, proof_requested_at_utc DESC, booking_id);
            """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Delivery proof request materialisation is forward-only.");
}
