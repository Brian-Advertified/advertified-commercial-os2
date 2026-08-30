using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class CampaignDeliveryProof
{
    private static void ReplaceDeliveryCampaignTransitionBoundary(
        MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
            """
            CREATE OR REPLACE FUNCTION commercial.enforce_campaign_transition()
            RETURNS trigger LANGUAGE plpgsql AS $campaign_transition$
            DECLARE expected record;
            DECLARE requirement_count integer;
            DECLARE booking_count integer;
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

                IF OLD.status_code = 'PLANNED' AND NEW.status_code = 'BOOKED' THEN
                    IF NEW.bookings_confirmed_by <> commercial.current_user_id()
                       OR NEW.version <> OLD.version + 1
                       OR NEW.updated_at_utc < OLD.updated_at_utc
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
                END IF;

                IF OLD.status_code = 'BOOKED' AND NEW.status_code = 'CREATIVE_PENDING' THEN
                    SELECT count(*)::integer INTO requirement_count
                    FROM commercial.creative_requirements requirement
                    WHERE requirement.buyer_tenant_id = OLD.tenant_id
                      AND requirement.campaign_id = OLD.id;
                    SELECT count(*)::integer INTO booking_count
                    FROM commercial.bookings booking
                    WHERE booking.buyer_tenant_id = OLD.tenant_id
                      AND booking.proposal_decision_id = OLD.proposal_decision_id
                      AND booking.plan_version_id = OLD.plan_version_id
                      AND booking.status_code = 'CONFIRMED';
                    IF (NEW.bookings_confirmed_by, NEW.bookings_confirmed_at_utc,
                        NEW.booking_confirmation_reason) IS DISTINCT FROM
                       (OLD.bookings_confirmed_by, OLD.bookings_confirmed_at_utc,
                        OLD.booking_confirmation_reason)
                       OR NEW.creative_requested_by <> commercial.current_user_id()
                       OR NEW.version <> OLD.version + 1
                       OR NEW.updated_at_utc < OLD.updated_at_utc
                       OR requirement_count <= 0 OR requirement_count <> booking_count
                       OR EXISTS (
                           SELECT 1 FROM commercial.bookings booking
                           WHERE booking.buyer_tenant_id = OLD.tenant_id
                             AND booking.proposal_decision_id = OLD.proposal_decision_id
                             AND booking.plan_version_id = OLD.plan_version_id
                             AND booking.status_code = 'CONFIRMED'
                             AND NOT EXISTS (
                                 SELECT 1 FROM commercial.creative_requirements requirement
                                 WHERE requirement.buyer_tenant_id = OLD.tenant_id
                                   AND requirement.campaign_id = OLD.id
                                   AND requirement.booking_id = booking.id)) THEN
                        RAISE EXCEPTION 'creative requirements do not cover confirmed bookings';
                    END IF;
                    RETURN NEW;
                END IF;

                IF OLD.status_code = 'CREATIVE_PENDING' AND NEW.status_code = 'READY' THEN
                    IF (NEW.bookings_confirmed_by, NEW.bookings_confirmed_at_utc,
                        NEW.booking_confirmation_reason, NEW.creative_requested_by,
                        NEW.creative_requested_at_utc, NEW.creative_request_reason)
                       IS DISTINCT FROM
                       (OLD.bookings_confirmed_by, OLD.bookings_confirmed_at_utc,
                        OLD.booking_confirmation_reason, OLD.creative_requested_by,
                        OLD.creative_requested_at_utc, OLD.creative_request_reason)
                       OR NEW.creative_approved_by <> commercial.current_user_id()
                       OR NEW.version <> OLD.version + 1
                       OR NEW.updated_at_utc < OLD.updated_at_utc
                       OR NOT EXISTS (
                           SELECT 1 FROM commercial.creative_requirements requirement
                           WHERE requirement.buyer_tenant_id = OLD.tenant_id
                             AND requirement.campaign_id = OLD.id)
                       OR EXISTS (
                           SELECT 1 FROM commercial.creative_requirements requirement
                           WHERE requirement.buyer_tenant_id = OLD.tenant_id
                             AND requirement.campaign_id = OLD.id
                             AND NOT EXISTS (
                                 SELECT 1 FROM commercial.creative_assets asset
                                 JOIN commercial.creative_asset_reviews brand
                                   ON brand.buyer_tenant_id = asset.buyer_tenant_id
                                  AND brand.asset_version_id = asset.current_version_id
                                  AND brand.review_type_code = 'BRAND_LEGAL_RIGHTS'
                                  AND brand.decision_code = 'APPROVED'
                                  AND brand.rights_status_code = 'APPROVED'
                                 JOIN commercial.creative_asset_reviews supplier
                                   ON supplier.buyer_tenant_id = asset.buyer_tenant_id
                                  AND supplier.asset_version_id = asset.current_version_id
                                  AND supplier.review_type_code = 'SUPPLIER_TECHNICAL'
                                  AND supplier.decision_code = 'APPROVED'
                                 WHERE asset.buyer_tenant_id = OLD.tenant_id
                                   AND asset.requirement_id = requirement.id
                                   AND asset.current_version_id IS NOT NULL)) THEN
                        RAISE EXCEPTION 'campaign creative is not ready';
                    END IF;
                    RETURN NEW;
                END IF;

                IF OLD.status_code = 'READY' AND NEW.status_code = 'LIVE' THEN
                    IF (NEW.bookings_confirmed_by, NEW.bookings_confirmed_at_utc,
                        NEW.booking_confirmation_reason, NEW.creative_requested_by,
                        NEW.creative_requested_at_utc, NEW.creative_request_reason,
                        NEW.creative_approved_by, NEW.creative_approved_at_utc,
                        NEW.creative_approval_reason) IS DISTINCT FROM
                       (OLD.bookings_confirmed_by, OLD.bookings_confirmed_at_utc,
                        OLD.booking_confirmation_reason, OLD.creative_requested_by,
                        OLD.creative_requested_at_utc, OLD.creative_request_reason,
                        OLD.creative_approved_by, OLD.creative_approved_at_utc,
                        OLD.creative_approval_reason)
                       OR NEW.started_by <> commercial.current_user_id()
                       OR (NEW.started_at_utc AT TIME ZONE 'UTC')::date
                            NOT BETWEEN OLD.start_date AND OLD.end_date
                       OR NEW.version <> OLD.version + 1
                       OR NEW.updated_at_utc <> NEW.started_at_utc
                       OR NOT EXISTS (
                           SELECT 1 FROM commercial.payment_intents payment
                           WHERE payment.tenant_id = OLD.tenant_id
                             AND payment.id = OLD.payment_intent_id
                             AND payment.status_code = 'CONFIRMED')
                       OR EXISTS (
                           SELECT 1 FROM commercial.media_plan_lines line
                           WHERE line.tenant_id = OLD.tenant_id
                             AND line.plan_version_id = OLD.plan_version_id
                             AND NOT EXISTS (
                                 SELECT 1 FROM commercial.bookings booking
                                 WHERE booking.buyer_tenant_id = OLD.tenant_id
                                   AND booking.proposal_decision_id = OLD.proposal_decision_id
                                   AND booking.media_plan_line_id = line.id
                                   AND booking.status_code = 'CONFIRMED'))
                       OR EXISTS (
                           SELECT 1 FROM commercial.creative_requirements requirement
                           WHERE requirement.buyer_tenant_id = OLD.tenant_id
                             AND requirement.campaign_id = OLD.id
                             AND NOT EXISTS (
                                 SELECT 1 FROM commercial.creative_assets asset
                                 JOIN commercial.creative_asset_reviews brand
                                   ON brand.buyer_tenant_id = asset.buyer_tenant_id
                                  AND brand.asset_version_id = asset.current_version_id
                                  AND brand.review_type_code = 'BRAND_LEGAL_RIGHTS'
                                  AND brand.decision_code = 'APPROVED'
                                  AND brand.rights_status_code = 'APPROVED'
                                 JOIN commercial.creative_asset_reviews supplier
                                   ON supplier.buyer_tenant_id = asset.buyer_tenant_id
                                  AND supplier.asset_version_id = asset.current_version_id
                                  AND supplier.review_type_code = 'SUPPLIER_TECHNICAL'
                                  AND supplier.decision_code = 'APPROVED'
                                 WHERE asset.buyer_tenant_id = OLD.tenant_id
                                   AND asset.requirement_id = requirement.id
                                   AND asset.current_version_id IS NOT NULL)) THEN
                        RAISE EXCEPTION 'campaign delivery dependencies are not ready';
                    END IF;
                    RETURN NEW;
                END IF;

                IF OLD.status_code = 'LIVE' AND NEW.status_code = 'COMPLETED' THEN
                    IF (NEW.bookings_confirmed_by, NEW.bookings_confirmed_at_utc,
                        NEW.booking_confirmation_reason, NEW.creative_requested_by,
                        NEW.creative_requested_at_utc, NEW.creative_request_reason,
                        NEW.creative_approved_by, NEW.creative_approved_at_utc,
                        NEW.creative_approval_reason, NEW.started_by,
                        NEW.started_at_utc, NEW.start_reason) IS DISTINCT FROM
                       (OLD.bookings_confirmed_by, OLD.bookings_confirmed_at_utc,
                        OLD.booking_confirmation_reason, OLD.creative_requested_by,
                        OLD.creative_requested_at_utc, OLD.creative_request_reason,
                        OLD.creative_approved_by, OLD.creative_approved_at_utc,
                        OLD.creative_approval_reason, OLD.started_by,
                        OLD.started_at_utc, OLD.start_reason)
                       OR NEW.completed_by <> commercial.current_user_id()
                       OR NEW.proof_requested_by <> commercial.current_user_id()
                       OR NEW.proof_requested_at_utc <> NEW.completed_at_utc
                       OR (NEW.completed_at_utc AT TIME ZONE 'UTC')::date <= OLD.end_date
                       OR NEW.version <> OLD.version + 1
                       OR NEW.updated_at_utc <> NEW.completed_at_utc THEN
                        RAISE EXCEPTION 'campaign delivery is not complete';
                    END IF;
                    RETURN NEW;
                END IF;

                RAISE EXCEPTION 'campaign lifecycle transition is invalid';
            END;
            $campaign_transition$;
            """);
}
