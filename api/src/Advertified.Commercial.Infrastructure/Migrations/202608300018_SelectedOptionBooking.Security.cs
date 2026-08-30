using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class SelectedOptionBooking
{
    private static void CreateBookingSecurityBoundary(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.bookings ENABLE ROW LEVEL SECURITY;
            ALTER TABLE commercial.bookings FORCE ROW LEVEL SECURITY;
            CREATE POLICY bookings_read ON commercial.bookings FOR SELECT USING (
                buyer_tenant_id = commercial.current_tenant_id()
                OR supplier_tenant_id = commercial.current_tenant_id());
            CREATE POLICY bookings_insert ON commercial.bookings FOR INSERT WITH CHECK (
                buyer_tenant_id = commercial.current_tenant_id());
            CREATE POLICY bookings_update ON commercial.bookings FOR UPDATE USING (
                buyer_tenant_id = commercial.current_tenant_id()
                OR supplier_tenant_id = commercial.current_tenant_id())
                WITH CHECK (
                    buyer_tenant_id = commercial.current_tenant_id()
                    OR supplier_tenant_id = commercial.current_tenant_id());

            CREATE FUNCTION commercial.enforce_booking_transition()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $booking_transition$
            BEGIN
                IF TG_OP = 'DELETE' THEN
                    RAISE EXCEPTION 'booking records cannot be deleted';
                END IF;
                IF (NEW.id, NEW.buyer_tenant_id, NEW.supplier_tenant_id,
                    NEW.proposal_version_id, NEW.proposal_option_id,
                    NEW.proposal_decision_id, NEW.plan_version_id,
                    NEW.media_plan_line_id, NEW.marketplace_listing_version_id,
                    NEW.commercial_policy_version_id, NEW.supplier_id,
                    NEW.inventory_product_id, NEW.product_version_id,
                    NEW.rate_id, NEW.availability_id, NEW.supplier_name,
                    NEW.product_name, NEW.channel_code, NEW.geography,
                    NEW.flight_start, NEW.flight_end, NEW.running_periods,
                    NEW.quantity, NEW.supplier_cost_minor, NEW.markup_minor,
                    NEW.commission_minor, NEW.management_fee_minor,
                    NEW.client_price_minor, NEW.fees_minor, NEW.vat_minor,
                    NEW.booking_approval_threshold_minor,
                    NEW.currency_collection_code, NEW.currency_code,
                    NEW.terms, NEW.created_by, NEW.created_at_utc)
                    IS DISTINCT FROM
                   (OLD.id, OLD.buyer_tenant_id, OLD.supplier_tenant_id,
                    OLD.proposal_version_id, OLD.proposal_option_id,
                    OLD.proposal_decision_id, OLD.plan_version_id,
                    OLD.media_plan_line_id, OLD.marketplace_listing_version_id,
                    OLD.commercial_policy_version_id, OLD.supplier_id,
                    OLD.inventory_product_id, OLD.product_version_id,
                    OLD.rate_id, OLD.availability_id, OLD.supplier_name,
                    OLD.product_name, OLD.channel_code, OLD.geography,
                    OLD.flight_start, OLD.flight_end, OLD.running_periods,
                    OLD.quantity, OLD.supplier_cost_minor, OLD.markup_minor,
                    OLD.commission_minor, OLD.management_fee_minor,
                    OLD.client_price_minor, OLD.fees_minor, OLD.vat_minor,
                    OLD.booking_approval_threshold_minor,
                    OLD.currency_collection_code, OLD.currency_code,
                    OLD.terms, OLD.created_by, OLD.created_at_utc) THEN
                    RAISE EXCEPTION 'booking commercial snapshot is immutable';
                END IF;
                IF NEW.version <> OLD.version + 1 OR NEW.updated_at_utc < OLD.updated_at_utc THEN
                    RAISE EXCEPTION 'invalid booking version transition';
                END IF;
                IF OLD.status_code = 'DRAFT' AND NEW.status_code = 'PENDING_SUPPLIER' THEN
                    IF commercial.current_tenant_id() <> OLD.buyer_tenant_id
                       OR NEW.requested_by <> commercial.current_user_id()
                       OR NEW.requested_at_utc IS NULL
                       OR btrim(COALESCE(NEW.request_reason, '')) = ''
                       OR NEW.confirmed_by IS NOT NULL OR NEW.confirmed_at_utc IS NOT NULL
                       OR NEW.confirmation_reason IS NOT NULL OR NEW.supplier_note IS NOT NULL
                       OR NEW.terms_accepted THEN
                        RAISE EXCEPTION 'invalid buyer booking request';
                    END IF;
                    RETURN NEW;
                END IF;
                IF OLD.status_code = 'PENDING_SUPPLIER' AND NEW.status_code = 'CONFIRMED' THEN
                    IF commercial.current_tenant_id() <> OLD.supplier_tenant_id
                       OR NEW.confirmed_by <> commercial.current_user_id()
                       OR NEW.confirmed_at_utc IS NULL
                       OR btrim(COALESCE(NEW.confirmation_reason, '')) = ''
                       OR NOT NEW.terms_accepted
                       OR (NEW.requested_by, NEW.requested_at_utc, NEW.request_reason)
                          IS DISTINCT FROM
                          (OLD.requested_by, OLD.requested_at_utc, OLD.request_reason) THEN
                        RAISE EXCEPTION 'invalid supplier booking confirmation';
                    END IF;
                    RETURN NEW;
                END IF;
                RAISE EXCEPTION 'invalid booking lifecycle transition';
            END;
            $booking_transition$;

            CREATE TRIGGER protect_booking_transition
                BEFORE UPDATE OR DELETE ON commercial.bookings
                FOR EACH ROW EXECUTE FUNCTION commercial.enforce_booking_transition();

            GRANT SELECT, INSERT, UPDATE ON commercial.bookings TO advertified_app;
            """);
}
