using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class CreativeProductionReadiness
{
    private static void CreateCreativeSecurityBoundary(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.creative_requirements ENABLE ROW LEVEL SECURITY;
            ALTER TABLE commercial.creative_requirements FORCE ROW LEVEL SECURITY;
            CREATE POLICY creative_requirements_participant_scope
                ON commercial.creative_requirements
                USING (buyer_tenant_id = commercial.current_tenant_id()
                    OR supplier_tenant_id = commercial.current_tenant_id())
                WITH CHECK (buyer_tenant_id = commercial.current_tenant_id());

            ALTER TABLE commercial.creative_assets ENABLE ROW LEVEL SECURITY;
            ALTER TABLE commercial.creative_assets FORCE ROW LEVEL SECURITY;
            CREATE POLICY creative_assets_participant_scope ON commercial.creative_assets
                USING (buyer_tenant_id = commercial.current_tenant_id()
                    OR supplier_tenant_id = commercial.current_tenant_id())
                WITH CHECK (buyer_tenant_id = commercial.current_tenant_id());
            CREATE POLICY creative_assets_supplier_review_update
                ON commercial.creative_assets FOR UPDATE
                USING (supplier_tenant_id = commercial.current_tenant_id())
                WITH CHECK (supplier_tenant_id = commercial.current_tenant_id());

            ALTER TABLE commercial.creative_asset_versions ENABLE ROW LEVEL SECURITY;
            ALTER TABLE commercial.creative_asset_versions FORCE ROW LEVEL SECURITY;
            CREATE POLICY creative_versions_participant_scope
                ON commercial.creative_asset_versions
                USING (buyer_tenant_id = commercial.current_tenant_id()
                    OR supplier_tenant_id = commercial.current_tenant_id())
                WITH CHECK (buyer_tenant_id = commercial.current_tenant_id());

            ALTER TABLE commercial.creative_asset_reviews ENABLE ROW LEVEL SECURITY;
            ALTER TABLE commercial.creative_asset_reviews FORCE ROW LEVEL SECURITY;
            CREATE POLICY creative_reviews_participant_scope
                ON commercial.creative_asset_reviews
                USING (buyer_tenant_id = commercial.current_tenant_id()
                    OR supplier_tenant_id = commercial.current_tenant_id())
                WITH CHECK (
                    (review_type_code = 'BRAND_LEGAL_RIGHTS'
                        AND buyer_tenant_id = commercial.current_tenant_id())
                    OR (review_type_code = 'SUPPLIER_TECHNICAL'
                        AND supplier_tenant_id = commercial.current_tenant_id()));

            CREATE FUNCTION commercial.enforce_creative_requirement()
            RETURNS trigger LANGUAGE plpgsql AS $creative_requirement$
            DECLARE expected record;
            BEGIN
                IF TG_OP <> 'INSERT' THEN
                    RAISE EXCEPTION 'creative requirements are immutable';
                END IF;
                SELECT campaign.status_code AS campaign_status,
                    booking.supplier_tenant_id, booking.media_plan_line_id,
                    booking.channel_code, booking.flight_start, booking.flight_end,
                    booking.status_code AS booking_status
                INTO expected
                FROM commercial.campaigns campaign
                JOIN commercial.bookings booking
                  ON booking.buyer_tenant_id = campaign.tenant_id
                 AND booking.proposal_decision_id = campaign.proposal_decision_id
                 AND booking.plan_version_id = campaign.plan_version_id
                WHERE campaign.tenant_id = NEW.buyer_tenant_id
                  AND campaign.id = NEW.campaign_id
                  AND booking.id = NEW.booking_id;
                IF NOT FOUND OR NEW.buyer_tenant_id <> commercial.current_tenant_id()
                   OR NEW.created_by <> commercial.current_user_id()
                   OR expected.campaign_status <> 'BOOKED'
                   OR expected.booking_status <> 'CONFIRMED'
                   OR (NEW.supplier_tenant_id, NEW.media_plan_line_id,
                       NEW.channel_code, NEW.flight_start, NEW.flight_end)
                      IS DISTINCT FROM
                      (expected.supplier_tenant_id, expected.media_plan_line_id,
                       expected.channel_code, expected.flight_start, expected.flight_end) THEN
                    RAISE EXCEPTION 'creative requirement does not match a confirmed booking';
                END IF;
                RETURN NEW;
            END;
            $creative_requirement$;

            CREATE TRIGGER protect_creative_requirement
                BEFORE INSERT OR UPDATE OR DELETE ON commercial.creative_requirements
                FOR EACH ROW EXECUTE FUNCTION commercial.enforce_creative_requirement();

            CREATE FUNCTION commercial.enforce_creative_asset()
            RETURNS trigger LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, commercial
            AS $creative_asset$
            DECLARE campaign_status varchar(100);
            BEGIN
                IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'creative assets cannot be deleted'; END IF;
                SELECT campaign.status_code INTO campaign_status
                FROM commercial.campaigns campaign
                WHERE campaign.tenant_id = NEW.buyer_tenant_id
                  AND campaign.id = NEW.campaign_id;
                IF TG_OP = 'INSERT' THEN
                    IF NEW.buyer_tenant_id <> commercial.current_tenant_id()
                       OR NEW.created_by <> commercial.current_user_id()
                       OR NEW.current_version_id IS NOT NULL OR NEW.version <> 0
                       OR campaign_status <> 'CREATIVE_PENDING' THEN
                        RAISE EXCEPTION 'creative asset cannot be created';
                    END IF;
                    RETURN NEW;
                END IF;
                IF campaign_status <> 'CREATIVE_PENDING'
                   OR (NEW.id, NEW.buyer_tenant_id, NEW.supplier_tenant_id,
                       NEW.campaign_id, NEW.requirement_id, NEW.created_by, NEW.created_at_utc)
                      IS DISTINCT FROM
                      (OLD.id, OLD.buyer_tenant_id, OLD.supplier_tenant_id,
                       OLD.campaign_id, OLD.requirement_id, OLD.created_by, OLD.created_at_utc)
                   OR NEW.version <> OLD.version + 1
                   OR NEW.updated_at_utc < OLD.updated_at_utc THEN
                    RAISE EXCEPTION 'creative asset version pointer is invalid';
                END IF;
                IF NEW.current_version_id IS DISTINCT FROM OLD.current_version_id THEN
                    IF NEW.buyer_tenant_id <> commercial.current_tenant_id()
                       OR NOT EXISTS (
                       SELECT 1 FROM commercial.creative_asset_versions version
                       LEFT JOIN commercial.creative_asset_versions prior
                         ON prior.buyer_tenant_id = OLD.buyer_tenant_id
                        AND prior.id = OLD.current_version_id
                       WHERE version.buyer_tenant_id = NEW.buyer_tenant_id
                         AND version.supplier_tenant_id = NEW.supplier_tenant_id
                         AND version.id = NEW.current_version_id
                         AND version.asset_id = NEW.id
                         AND version.requirement_id = NEW.requirement_id
                         AND version.version_number = COALESCE(prior.version_number, 0) + 1) THEN
                        RAISE EXCEPTION 'creative asset version pointer is invalid';
                    END IF;
                ELSIF NEW.version <> (
                    SELECT count(*) FROM commercial.creative_asset_versions version
                    WHERE version.buyer_tenant_id = NEW.buyer_tenant_id
                      AND version.asset_id = NEW.id) + (
                    SELECT count(*) FROM commercial.creative_asset_reviews review
                    WHERE review.buyer_tenant_id = NEW.buyer_tenant_id
                      AND review.asset_id = NEW.id)
                   OR NOT EXISTS (
                    SELECT 1 FROM commercial.creative_asset_reviews review
                    WHERE review.buyer_tenant_id = NEW.buyer_tenant_id
                      AND review.asset_id = NEW.id
                      AND review.asset_version_id = NEW.current_version_id
                      AND review.reviewed_by = commercial.current_user_id()
                      AND review.reviewer_tenant_id = commercial.current_tenant_id()
                      AND review.reviewed_at_utc = NEW.updated_at_utc) THEN
                    RAISE EXCEPTION 'creative review does not match the asset update';
                END IF;
                RETURN NEW;
            END;
            $creative_asset$;

            CREATE TRIGGER protect_creative_asset
                BEFORE INSERT OR UPDATE OR DELETE ON commercial.creative_assets
                FOR EACH ROW EXECUTE FUNCTION commercial.enforce_creative_asset();

            REVOKE ALL ON FUNCTION commercial.enforce_creative_asset() FROM PUBLIC;
            """);
}
