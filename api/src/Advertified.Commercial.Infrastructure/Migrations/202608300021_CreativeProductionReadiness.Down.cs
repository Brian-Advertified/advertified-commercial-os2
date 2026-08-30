using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class CreativeProductionReadiness
{
    private static void DropCreativeBoundary(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            DROP TRIGGER IF EXISTS protect_creative_asset_review
                ON commercial.creative_asset_reviews;
            DROP FUNCTION IF EXISTS commercial.enforce_creative_asset_review();
            DROP TRIGGER IF EXISTS protect_creative_asset_version
                ON commercial.creative_asset_versions;
            DROP FUNCTION IF EXISTS commercial.enforce_creative_asset_version();
            DROP TRIGGER IF EXISTS protect_creative_asset ON commercial.creative_assets;
            DROP FUNCTION IF EXISTS commercial.enforce_creative_asset();
            DROP TRIGGER IF EXISTS protect_creative_requirement
                ON commercial.creative_requirements;
            DROP FUNCTION IF EXISTS commercial.enforce_creative_requirement();
            DROP TABLE commercial.creative_asset_reviews;
            ALTER TABLE commercial.creative_assets
                DROP CONSTRAINT fk_creative_asset_current_version;
            DROP TABLE commercial.creative_asset_versions;
            DROP TABLE commercial.creative_assets;
            DROP TABLE commercial.creative_requirements;
            ALTER TABLE commercial.campaigns
                DROP CONSTRAINT ck_campaign_status_shape,
                DROP CONSTRAINT fk_campaign_creative_requester,
                DROP CONSTRAINT fk_campaign_creative_approver,
                DROP COLUMN creative_requested_by,
                DROP COLUMN creative_requested_at_utc,
                DROP COLUMN creative_request_reason,
                DROP COLUMN creative_approved_by,
                DROP COLUMN creative_approved_at_utc,
                DROP COLUMN creative_approval_reason,
                ADD CONSTRAINT ck_campaign_status_shape CHECK (
                    (status_code = 'PLANNED' AND bookings_confirmed_by IS NULL
                        AND bookings_confirmed_at_utc IS NULL
                        AND booking_confirmation_reason IS NULL)
                    OR (status_code = 'BOOKED' AND bookings_confirmed_by IS NOT NULL
                        AND bookings_confirmed_at_utc IS NOT NULL
                        AND btrim(COALESCE(booking_confirmation_reason, '')) <> ''));
            """);
}
