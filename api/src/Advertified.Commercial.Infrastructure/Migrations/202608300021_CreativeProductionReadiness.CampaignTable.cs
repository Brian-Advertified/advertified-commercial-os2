using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class CreativeProductionReadiness
{
    private static void AddCampaignCreativeState(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.campaigns
                DROP CONSTRAINT ck_campaign_status_shape,
                ADD COLUMN creative_requested_by uuid,
                ADD COLUMN creative_requested_at_utc timestamptz,
                ADD COLUMN creative_request_reason varchar(1000),
                ADD COLUMN creative_approved_by uuid,
                ADD COLUMN creative_approved_at_utc timestamptz,
                ADD COLUMN creative_approval_reason varchar(1000),
                ADD CONSTRAINT fk_campaign_creative_requester FOREIGN KEY (creative_requested_by)
                    REFERENCES commercial.users (id),
                ADD CONSTRAINT fk_campaign_creative_approver FOREIGN KEY (creative_approved_by)
                    REFERENCES commercial.users (id),
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
                    OR (status_code = 'CREATIVE_PENDING' AND bookings_confirmed_by IS NOT NULL
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
