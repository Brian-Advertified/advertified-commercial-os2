using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202608310025_SupplierDeliveryProofRequests")]
public sealed class SupplierDeliveryProofRequests : Migration
{
    private const int MaximumRequestCount = 200;

    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        $$"""
        ALTER TABLE commercial.delivery_proofs
            ADD COLUMN submission_sequence bigint GENERATED ALWAYS AS IDENTITY;
        ALTER TABLE commercial.delivery_proofs
            ADD CONSTRAINT ux_delivery_proof_submission_sequence
            UNIQUE (submission_sequence);
        CREATE UNIQUE INDEX ux_delivery_proof_active_booking
            ON commercial.delivery_proofs (
                buyer_tenant_id, supplier_tenant_id, booking_id)
            WHERE status_code IN ('SUBMITTED', 'APPROVED');

        CREATE FUNCTION commercial.supplier_delivery_proof_requests()
        RETURNS TABLE (
            campaign_id uuid,
            booking_id uuid,
            supplier_name text,
            product_name text,
            channel_code text,
            geography text,
            flight_start date,
            flight_end date,
            proof_requested_at_utc timestamptz,
            proof_request_reason text,
            latest_proof_id uuid,
            latest_proof_status text)
        LANGUAGE sql STABLE SECURITY DEFINER
        SET search_path = pg_catalog
        AS $supplier_delivery_proof_requests$
            SELECT campaign.id,
                booking.id,
                booking.supplier_name,
                booking.product_name,
                booking.channel_code,
                booking.geography,
                booking.flight_start,
                booking.flight_end,
                campaign.proof_requested_at_utc,
                campaign.proof_request_reason,
                proof.id,
                proof.status_code
            FROM commercial.bookings booking
            JOIN commercial.campaigns campaign
              ON campaign.tenant_id = booking.buyer_tenant_id
             AND campaign.proposal_decision_id = booking.proposal_decision_id
             AND campaign.plan_version_id = booking.plan_version_id
            JOIN commercial.memberships membership
              ON membership.tenant_id = booking.supplier_tenant_id
             AND membership.user_id = commercial.current_user_id()
             AND membership.status_code = 'ACTIVE'
            JOIN commercial.users actor
              ON actor.id = membership.user_id
             AND actor.status_code = 'ACTIVE'
            JOIN commercial.tenants supplier_tenant
              ON supplier_tenant.id = membership.tenant_id
             AND supplier_tenant.status_code = 'ACTIVE'
            JOIN governance.master_data_items permission
              ON permission.collection_code = 'permissions'
             AND permission.code = 'delivery_proof_submit'
             AND permission.is_active
             AND permission.effective_from <= CURRENT_DATE
             AND (permission.effective_to IS NULL
                  OR CURRENT_DATE < permission.effective_to)
             AND pg_catalog.jsonb_exists(
                permission.metadata_json -> 'roles', membership.role_code)
            LEFT JOIN LATERAL (
                SELECT candidate.id, candidate.status_code,
                    candidate.submission_sequence
                FROM commercial.delivery_proofs candidate
                WHERE candidate.buyer_tenant_id = booking.buyer_tenant_id
                  AND candidate.campaign_id = campaign.id
                  AND candidate.booking_id = booking.id
                  AND candidate.supplier_tenant_id = booking.supplier_tenant_id
                ORDER BY candidate.submission_sequence DESC
                LIMIT 1) proof ON true
            WHERE booking.supplier_tenant_id = commercial.current_tenant_id()
              AND booking.status_code = 'CONFIRMED'
              AND campaign.status_code = 'COMPLETED'
              AND campaign.proof_requested_by IS NOT NULL
              AND campaign.proof_requested_at_utc IS NOT NULL
              AND btrim(campaign.proof_request_reason) <> ''
            ORDER BY campaign.proof_requested_at_utc DESC, booking.id
            LIMIT {{MaximumRequestCount}};
        $supplier_delivery_proof_requests$;

        REVOKE ALL ON FUNCTION commercial.supplier_delivery_proof_requests() FROM PUBLIC;
        GRANT EXECUTE ON FUNCTION commercial.supplier_delivery_proof_requests()
            TO advertified_app;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        """
        DROP FUNCTION IF EXISTS commercial.supplier_delivery_proof_requests();
        DROP INDEX IF EXISTS commercial.ux_delivery_proof_active_booking;
        ALTER TABLE commercial.delivery_proofs
            DROP CONSTRAINT IF EXISTS ux_delivery_proof_submission_sequence,
            DROP COLUMN IF EXISTS submission_sequence;
        """);
}
