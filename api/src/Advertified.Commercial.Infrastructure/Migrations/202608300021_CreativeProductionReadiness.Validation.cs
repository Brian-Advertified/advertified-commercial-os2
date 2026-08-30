using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class CreativeProductionReadiness
{
    private static void CreateCreativeValidationBoundary(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            CREATE FUNCTION commercial.enforce_creative_asset_version()
            RETURNS trigger LANGUAGE plpgsql AS $creative_version$
            DECLARE expected record;
            DECLARE expected_commercial jsonb;
            BEGIN
                IF TG_OP <> 'INSERT' THEN
                    RAISE EXCEPTION 'creative asset versions are immutable';
                END IF;
                SELECT current.version_number AS current_version_number, asset.requirement_id,
                    requirement.required_media_type, requirement.maximum_bytes,
                    campaign.status_code AS campaign_status, campaign.version AS campaign_version,
                    booking.version AS booking_version, booking.currency_code,
                    booking.supplier_cost_minor, booking.client_price_minor,
                    booking.fees_minor, booking.vat_minor
                INTO expected
                FROM commercial.creative_assets asset
                LEFT JOIN commercial.creative_asset_versions current
                  ON current.buyer_tenant_id = asset.buyer_tenant_id
                 AND current.id = asset.current_version_id
                JOIN commercial.creative_requirements requirement
                  ON requirement.buyer_tenant_id = asset.buyer_tenant_id
                 AND requirement.id = asset.requirement_id
                JOIN commercial.campaigns campaign
                  ON campaign.tenant_id = asset.buyer_tenant_id
                 AND campaign.id = asset.campaign_id
                JOIN commercial.bookings booking
                  ON booking.buyer_tenant_id = requirement.buyer_tenant_id
                 AND booking.supplier_tenant_id = requirement.supplier_tenant_id
                 AND booking.id = requirement.booking_id
                WHERE asset.buyer_tenant_id = NEW.buyer_tenant_id
                  AND asset.supplier_tenant_id = NEW.supplier_tenant_id
                  AND asset.id = NEW.asset_id;
                expected_commercial := jsonb_build_object(
                    'currency', expected.currency_code,
                    'supplierCostMinor', expected.supplier_cost_minor,
                    'clientPriceMinor', expected.client_price_minor,
                    'feesMinor', expected.fees_minor,
                    'vatMinor', expected.vat_minor);
                IF NOT FOUND OR NEW.buyer_tenant_id <> commercial.current_tenant_id()
                   OR NEW.created_by <> commercial.current_user_id()
                   OR expected.campaign_status <> 'CREATIVE_PENDING'
                   OR NEW.requirement_id <> expected.requirement_id
                   OR NEW.version_number <> COALESCE(expected.current_version_number, 0) + 1
                   OR NEW.media_type <> expected.required_media_type
                   OR NEW.size_bytes > expected.maximum_bytes
                   OR NEW.campaign_version <> expected.campaign_version
                   OR NEW.booking_version <> expected.booking_version
                   OR NEW.commercial_snapshot_json IS DISTINCT FROM expected_commercial
                   OR btrim(NEW.approved_copy) = '' THEN
                    RAISE EXCEPTION 'creative asset version does not match its requirement';
                END IF;
                RETURN NEW;
            END;
            $creative_version$;

            CREATE TRIGGER protect_creative_asset_version
                BEFORE INSERT OR UPDATE OR DELETE ON commercial.creative_asset_versions
                FOR EACH ROW EXECUTE FUNCTION commercial.enforce_creative_asset_version();

            CREATE FUNCTION commercial.enforce_creative_asset_review()
            RETURNS trigger LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, commercial
            AS $creative_review$
            DECLARE expected record;
            BEGIN
                IF TG_OP <> 'INSERT' THEN
                    RAISE EXCEPTION 'creative reviews are immutable';
                END IF;
                SELECT asset.current_version_id, campaign.status_code AS campaign_status
                INTO expected
                FROM commercial.creative_assets asset
                JOIN commercial.campaigns campaign
                  ON campaign.tenant_id = asset.buyer_tenant_id
                 AND campaign.id = asset.campaign_id
                WHERE asset.buyer_tenant_id = NEW.buyer_tenant_id
                  AND asset.supplier_tenant_id = NEW.supplier_tenant_id
                  AND asset.id = NEW.asset_id;
                IF NOT FOUND OR NEW.reviewed_by <> commercial.current_user_id()
                   OR NEW.reviewer_tenant_id <> commercial.current_tenant_id()
                   OR expected.campaign_status <> 'CREATIVE_PENDING'
                   OR NEW.asset_version_id <> expected.current_version_id
                   OR (NEW.review_type_code = 'BRAND_LEGAL_RIGHTS'
                       AND NEW.buyer_tenant_id <> commercial.current_tenant_id())
                   OR (NEW.review_type_code = 'SUPPLIER_TECHNICAL'
                       AND NEW.supplier_tenant_id <> commercial.current_tenant_id()) THEN
                    RAISE EXCEPTION 'creative review is not authorised for the current version';
                END IF;
                RETURN NEW;
            END;
            $creative_review$;

            CREATE TRIGGER protect_creative_asset_review
                BEFORE INSERT OR UPDATE OR DELETE ON commercial.creative_asset_reviews
                FOR EACH ROW EXECUTE FUNCTION commercial.enforce_creative_asset_review();

            REVOKE ALL ON FUNCTION commercial.enforce_creative_asset_review() FROM PUBLIC;

            GRANT SELECT, INSERT ON commercial.creative_requirements TO advertified_app;
            GRANT SELECT, INSERT, UPDATE ON commercial.creative_assets TO advertified_app;
            GRANT SELECT, INSERT ON commercial.creative_asset_versions TO advertified_app;
            GRANT SELECT, INSERT ON commercial.creative_asset_reviews TO advertified_app;
            """);
}
