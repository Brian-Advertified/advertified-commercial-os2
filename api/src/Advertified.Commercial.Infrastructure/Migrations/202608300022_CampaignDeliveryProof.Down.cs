using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class CampaignDeliveryProof
{
    private static void GuardDeliveryRollback(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            DO $delivery_rollback_guard$
            BEGIN
                IF EXISTS (SELECT 1 FROM commercial.delivery_proofs)
                   OR EXISTS (
                       SELECT 1 FROM commercial.campaigns
                       WHERE status_code IN ('LIVE', 'COMPLETED')) THEN
                    RAISE EXCEPTION
                        'delivery proof migration cannot roll back while delivery records exist';
                END IF;
            END;
            $delivery_rollback_guard$;
            """);

    private static void DropDeliveryProofBoundary(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            DROP TRIGGER IF EXISTS apply_delivery_proof_task ON commercial.delivery_proofs;
            DROP FUNCTION IF EXISTS commercial.apply_delivery_proof_task();
            DROP TRIGGER IF EXISTS protect_delivery_proof ON commercial.delivery_proofs;
            DROP FUNCTION IF EXISTS commercial.enforce_delivery_proof();
            DROP FUNCTION IF EXISTS commercial.delivery_proof_source(uuid, uuid);
            DELETE FROM commercial.human_tasks
            WHERE task_type_code = 'DELIVERY_PROOF_REVIEW'
              AND resource_type_code = 'delivery_proof';
            DROP TABLE commercial.delivery_proofs;

            ALTER TABLE commercial.campaigns
                DROP CONSTRAINT ck_campaign_status_shape,
                DROP CONSTRAINT fk_campaign_starter,
                DROP CONSTRAINT fk_campaign_completer,
                DROP CONSTRAINT fk_campaign_proof_requester,
                DROP COLUMN started_by,
                DROP COLUMN started_at_utc,
                DROP COLUMN start_reason,
                DROP COLUMN completed_by,
                DROP COLUMN completed_at_utc,
                DROP COLUMN completion_reason,
                DROP COLUMN proof_requested_by,
                DROP COLUMN proof_requested_at_utc,
                DROP COLUMN proof_request_reason,
                ADD CONSTRAINT ck_campaign_status_shape CHECK (
                    (status_code = 'PLANNED' AND bookings_confirmed_by IS NULL
                        AND bookings_confirmed_at_utc IS NULL
                        AND booking_confirmation_reason IS NULL
                        AND creative_requested_by IS NULL AND creative_requested_at_utc IS NULL
                        AND creative_request_reason IS NULL AND creative_approved_by IS NULL
                        AND creative_approved_at_utc IS NULL AND creative_approval_reason IS NULL)
                    OR (status_code = 'BOOKED' AND bookings_confirmed_by IS NOT NULL
                        AND bookings_confirmed_at_utc IS NOT NULL
                        AND btrim(COALESCE(booking_confirmation_reason, '')) <> ''
                        AND creative_requested_by IS NULL AND creative_requested_at_utc IS NULL
                        AND creative_request_reason IS NULL AND creative_approved_by IS NULL
                        AND creative_approved_at_utc IS NULL AND creative_approval_reason IS NULL)
                    OR (status_code = 'CREATIVE_PENDING'
                        AND bookings_confirmed_by IS NOT NULL
                        AND bookings_confirmed_at_utc IS NOT NULL
                        AND btrim(COALESCE(booking_confirmation_reason, '')) <> ''
                        AND creative_requested_by IS NOT NULL
                        AND creative_requested_at_utc IS NOT NULL
                        AND btrim(COALESCE(creative_request_reason, '')) <> ''
                        AND creative_approved_by IS NULL AND creative_approved_at_utc IS NULL
                        AND creative_approval_reason IS NULL)
                    OR (status_code = 'READY' AND bookings_confirmed_by IS NOT NULL
                        AND bookings_confirmed_at_utc IS NOT NULL
                        AND btrim(COALESCE(booking_confirmation_reason, '')) <> ''
                        AND creative_requested_by IS NOT NULL
                        AND creative_requested_at_utc IS NOT NULL
                        AND btrim(COALESCE(creative_request_reason, '')) <> ''
                        AND creative_approved_by IS NOT NULL
                        AND creative_approved_at_utc IS NOT NULL
                        AND btrim(COALESCE(creative_approval_reason, '')) <> ''));
            """);
}
