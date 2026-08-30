using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class CampaignDeliveryProof
{
    private static void AddCampaignDeliveryState(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.campaigns
                DROP CONSTRAINT ck_campaign_status_shape,
                ADD COLUMN started_by uuid,
                ADD COLUMN started_at_utc timestamptz,
                ADD COLUMN start_reason varchar(1000),
                ADD COLUMN completed_by uuid,
                ADD COLUMN completed_at_utc timestamptz,
                ADD COLUMN completion_reason varchar(1000),
                ADD COLUMN proof_requested_by uuid,
                ADD COLUMN proof_requested_at_utc timestamptz,
                ADD COLUMN proof_request_reason varchar(1000),
                ADD CONSTRAINT fk_campaign_starter FOREIGN KEY (started_by)
                    REFERENCES commercial.users (id),
                ADD CONSTRAINT fk_campaign_completer FOREIGN KEY (completed_by)
                    REFERENCES commercial.users (id),
                ADD CONSTRAINT fk_campaign_proof_requester FOREIGN KEY (proof_requested_by)
                    REFERENCES commercial.users (id),
                ADD CONSTRAINT ck_campaign_status_shape CHECK (
                    (status_code = 'PLANNED' AND bookings_confirmed_by IS NULL
                        AND bookings_confirmed_at_utc IS NULL
                        AND booking_confirmation_reason IS NULL
                        AND creative_requested_by IS NULL AND creative_requested_at_utc IS NULL
                        AND creative_request_reason IS NULL AND creative_approved_by IS NULL
                        AND creative_approved_at_utc IS NULL AND creative_approval_reason IS NULL
                        AND started_by IS NULL AND started_at_utc IS NULL
                        AND start_reason IS NULL AND completed_by IS NULL
                        AND completed_at_utc IS NULL AND completion_reason IS NULL
                        AND proof_requested_by IS NULL AND proof_requested_at_utc IS NULL
                        AND proof_request_reason IS NULL)
                    OR (status_code = 'BOOKED' AND bookings_confirmed_by IS NOT NULL
                        AND bookings_confirmed_at_utc IS NOT NULL
                        AND btrim(COALESCE(booking_confirmation_reason, '')) <> ''
                        AND creative_requested_by IS NULL AND creative_requested_at_utc IS NULL
                        AND creative_request_reason IS NULL AND creative_approved_by IS NULL
                        AND creative_approved_at_utc IS NULL AND creative_approval_reason IS NULL
                        AND started_by IS NULL AND started_at_utc IS NULL
                        AND start_reason IS NULL AND completed_by IS NULL
                        AND completed_at_utc IS NULL AND completion_reason IS NULL
                        AND proof_requested_by IS NULL AND proof_requested_at_utc IS NULL
                        AND proof_request_reason IS NULL)
                    OR (status_code = 'CREATIVE_PENDING'
                        AND bookings_confirmed_by IS NOT NULL
                        AND bookings_confirmed_at_utc IS NOT NULL
                        AND btrim(COALESCE(booking_confirmation_reason, '')) <> ''
                        AND creative_requested_by IS NOT NULL
                        AND creative_requested_at_utc IS NOT NULL
                        AND btrim(COALESCE(creative_request_reason, '')) <> ''
                        AND creative_approved_by IS NULL AND creative_approved_at_utc IS NULL
                        AND creative_approval_reason IS NULL AND started_by IS NULL
                        AND started_at_utc IS NULL AND start_reason IS NULL
                        AND completed_by IS NULL AND completed_at_utc IS NULL
                        AND completion_reason IS NULL AND proof_requested_by IS NULL
                        AND proof_requested_at_utc IS NULL AND proof_request_reason IS NULL)
                    OR (status_code = 'READY' AND bookings_confirmed_by IS NOT NULL
                        AND bookings_confirmed_at_utc IS NOT NULL
                        AND btrim(COALESCE(booking_confirmation_reason, '')) <> ''
                        AND creative_requested_by IS NOT NULL
                        AND creative_requested_at_utc IS NOT NULL
                        AND btrim(COALESCE(creative_request_reason, '')) <> ''
                        AND creative_approved_by IS NOT NULL
                        AND creative_approved_at_utc IS NOT NULL
                        AND btrim(COALESCE(creative_approval_reason, '')) <> ''
                        AND started_by IS NULL AND started_at_utc IS NULL
                        AND start_reason IS NULL AND completed_by IS NULL
                        AND completed_at_utc IS NULL AND completion_reason IS NULL
                        AND proof_requested_by IS NULL AND proof_requested_at_utc IS NULL
                        AND proof_request_reason IS NULL)
                    OR (status_code = 'LIVE' AND bookings_confirmed_by IS NOT NULL
                        AND bookings_confirmed_at_utc IS NOT NULL
                        AND btrim(COALESCE(booking_confirmation_reason, '')) <> ''
                        AND creative_requested_by IS NOT NULL
                        AND creative_requested_at_utc IS NOT NULL
                        AND btrim(COALESCE(creative_request_reason, '')) <> ''
                        AND creative_approved_by IS NOT NULL
                        AND creative_approved_at_utc IS NOT NULL
                        AND btrim(COALESCE(creative_approval_reason, '')) <> ''
                        AND started_by IS NOT NULL AND started_at_utc IS NOT NULL
                        AND btrim(COALESCE(start_reason, '')) <> ''
                        AND completed_by IS NULL AND completed_at_utc IS NULL
                        AND completion_reason IS NULL AND proof_requested_by IS NULL
                        AND proof_requested_at_utc IS NULL AND proof_request_reason IS NULL)
                    OR (status_code = 'COMPLETED' AND bookings_confirmed_by IS NOT NULL
                        AND bookings_confirmed_at_utc IS NOT NULL
                        AND btrim(COALESCE(booking_confirmation_reason, '')) <> ''
                        AND creative_requested_by IS NOT NULL
                        AND creative_requested_at_utc IS NOT NULL
                        AND btrim(COALESCE(creative_request_reason, '')) <> ''
                        AND creative_approved_by IS NOT NULL
                        AND creative_approved_at_utc IS NOT NULL
                        AND btrim(COALESCE(creative_approval_reason, '')) <> ''
                        AND started_by IS NOT NULL AND started_at_utc IS NOT NULL
                        AND btrim(COALESCE(start_reason, '')) <> ''
                        AND completed_by IS NOT NULL AND completed_at_utc IS NOT NULL
                        AND btrim(COALESCE(completion_reason, '')) <> ''
                        AND proof_requested_by IS NOT NULL
                        AND proof_requested_at_utc IS NOT NULL
                        AND btrim(COALESCE(proof_request_reason, '')) <> ''));
            """);
}
