using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class CampaignBookingReadiness
{
    private static void CreateCampaignSecurityBoundary(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.campaigns ENABLE ROW LEVEL SECURITY;
            ALTER TABLE commercial.campaigns FORCE ROW LEVEL SECURITY;
            CREATE POLICY campaigns_tenant_scope ON commercial.campaigns
                USING (tenant_id = commercial.current_tenant_id())
                WITH CHECK (tenant_id = commercial.current_tenant_id());

            CREATE FUNCTION commercial.enforce_booking_funding_order()
            RETURNS trigger LANGUAGE plpgsql AS $booking_funding_order$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM commercial.campaigns campaign
                    JOIN commercial.payment_intents payment
                      ON payment.tenant_id = campaign.tenant_id
                     AND payment.id = campaign.payment_intent_id
                    WHERE campaign.tenant_id = NEW.buyer_tenant_id
                      AND campaign.proposal_decision_id = NEW.proposal_decision_id
                      AND campaign.plan_version_id = NEW.plan_version_id
                      AND campaign.status_code = 'PLANNED'
                      AND payment.status_code = 'CONFIRMED') THEN
                    RAISE EXCEPTION 'confirmed funding is required before booking';
                END IF;
                RETURN NEW;
            END;
            $booking_funding_order$;

            CREATE TRIGGER enforce_booking_funding_order
                BEFORE INSERT ON commercial.bookings
                FOR EACH ROW EXECUTE FUNCTION commercial.enforce_booking_funding_order();

            CREATE FUNCTION commercial.enforce_campaign_transition()
            RETURNS trigger LANGUAGE plpgsql AS $campaign_transition$
            DECLARE expected record;
            BEGIN
                IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'campaigns cannot be deleted'; END IF;
                IF TG_OP = 'INSERT' THEN
                    SELECT payment.status_code AS payment_status,
                        proposal.status_code AS proposal_status,
                        proposal.brief_id, proposal.brief_version_id,
                        decision.id AS decision_id, decision.decision_code,
                        option.id AS option_id, option.plan_version_id,
                        plan.status_code AS plan_status,
                        min(line.flight_start) AS start_date,
                        max(line.flight_end) AS end_date,
                        brief.owner_user_id, brief_version.measurement_json,
                        count(line.id)::integer AS required_count
                    INTO expected
                    FROM commercial.payment_intents payment
                    JOIN commercial.proposal_versions proposal
                      ON proposal.tenant_id = payment.tenant_id
                     AND proposal.id = payment.proposal_version_id
                    JOIN commercial.proposal_decisions decision
                      ON decision.tenant_id = proposal.tenant_id
                     AND decision.proposal_version_id = proposal.id
                    JOIN commercial.proposal_options option
                      ON option.tenant_id = decision.tenant_id
                     AND option.id = decision.option_id
                     AND option.id = payment.proposal_option_id
                    JOIN commercial.media_plan_versions plan
                      ON plan.tenant_id = option.tenant_id
                     AND plan.id = option.plan_version_id
                    JOIN commercial.media_plan_lines line
                      ON line.tenant_id = option.tenant_id
                     AND line.plan_version_id = option.plan_version_id
                    JOIN commercial.campaign_briefs brief
                      ON brief.tenant_id = proposal.tenant_id AND brief.id = proposal.brief_id
                    JOIN commercial.brief_versions brief_version
                      ON brief_version.tenant_id = proposal.tenant_id
                     AND brief_version.id = proposal.brief_version_id
                    WHERE payment.tenant_id = NEW.tenant_id
                      AND payment.id = NEW.payment_intent_id
                    GROUP BY payment.status_code, proposal.status_code,
                        proposal.brief_id, proposal.brief_version_id, decision.id,
                        decision.decision_code, option.id, option.plan_version_id,
                        plan.status_code, brief.owner_user_id,
                        brief_version.measurement_json;
                    IF NOT FOUND OR expected.payment_status <> 'CONFIRMED'
                       OR expected.proposal_status <> 'SELECTED'
                       OR expected.decision_code <> 'SELECTED'
                       OR expected.plan_status <> 'APPROVED'
                       OR expected.required_count <= 0
                       OR (NEW.brief_id, NEW.brief_version_id,
                           NEW.proposal_decision_id, NEW.proposal_option_id,
                           NEW.plan_version_id, NEW.start_date, NEW.end_date,
                           NEW.owner_user_id, NEW.measurement_plan_json)
                          IS DISTINCT FROM
                          (expected.brief_id, expected.brief_version_id,
                           expected.decision_id, expected.option_id,
                           expected.plan_version_id, expected.start_date,
                           expected.end_date, expected.owner_user_id,
                           expected.measurement_json)
                       OR NEW.status_code <> 'PLANNED'
                       OR NEW.created_by <> commercial.current_user_id() THEN
                        RAISE EXCEPTION 'campaign does not reconcile to confirmed funding';
                    END IF;
                    RETURN NEW;
                END IF;

                IF (NEW.id, NEW.tenant_id, NEW.brief_id, NEW.brief_version_id,
                    NEW.proposal_version_id, NEW.proposal_option_id,
                    NEW.proposal_decision_id, NEW.plan_version_id,
                    NEW.payment_intent_id, NEW.title, NEW.start_date, NEW.end_date,
                    NEW.owner_user_id, NEW.measurement_plan_json,
                    NEW.created_by, NEW.created_at_utc) IS DISTINCT FROM
                   (OLD.id, OLD.tenant_id, OLD.brief_id, OLD.brief_version_id,
                    OLD.proposal_version_id, OLD.proposal_option_id,
                    OLD.proposal_decision_id, OLD.plan_version_id,
                    OLD.payment_intent_id, OLD.title, OLD.start_date, OLD.end_date,
                    OLD.owner_user_id, OLD.measurement_plan_json,
                    OLD.created_by, OLD.created_at_utc) THEN
                    RAISE EXCEPTION 'campaign source snapshot is immutable';
                END IF;
                IF OLD.status_code <> 'PLANNED' OR NEW.status_code <> 'BOOKED'
                   OR NEW.bookings_confirmed_by <> commercial.current_user_id()
                   OR NEW.version <> OLD.version + 1 OR NEW.updated_at_utc < OLD.updated_at_utc
                   OR EXISTS (
                       SELECT 1 FROM commercial.media_plan_lines line
                       WHERE line.tenant_id = OLD.tenant_id
                         AND line.plan_version_id = OLD.plan_version_id
                         AND NOT EXISTS (
                             SELECT 1 FROM commercial.bookings booking
                             WHERE booking.buyer_tenant_id = OLD.tenant_id
                               AND booking.proposal_decision_id = OLD.proposal_decision_id
                               AND booking.media_plan_line_id = line.id
                               AND booking.status_code = 'CONFIRMED')) THEN
                    RAISE EXCEPTION 'campaign bookings are not ready';
                END IF;
                RETURN NEW;
            END;
            $campaign_transition$;

            CREATE TRIGGER protect_campaign_transition
                BEFORE INSERT OR UPDATE OR DELETE ON commercial.campaigns
                FOR EACH ROW EXECUTE FUNCTION commercial.enforce_campaign_transition();

            GRANT SELECT, INSERT, UPDATE ON commercial.campaigns TO advertified_app;
            """);
}
