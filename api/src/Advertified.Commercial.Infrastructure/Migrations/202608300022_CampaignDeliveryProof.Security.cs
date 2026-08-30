using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class CampaignDeliveryProof
{
    private static void CreateDeliveryProofSecurityBoundary(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.delivery_proofs ENABLE ROW LEVEL SECURITY;
            ALTER TABLE commercial.delivery_proofs FORCE ROW LEVEL SECURITY;
            CREATE POLICY delivery_proof_select_participant
                ON commercial.delivery_proofs FOR SELECT
                USING (buyer_tenant_id = commercial.current_tenant_id()
                    OR supplier_tenant_id = commercial.current_tenant_id());
            CREATE POLICY delivery_proof_supplier_insert
                ON commercial.delivery_proofs FOR INSERT
                WITH CHECK (supplier_tenant_id = commercial.current_tenant_id()
                    AND submitter_tenant_id = commercial.current_tenant_id());
            CREATE POLICY delivery_proof_buyer_update
                ON commercial.delivery_proofs FOR UPDATE
                USING (buyer_tenant_id = commercial.current_tenant_id())
                WITH CHECK (buyer_tenant_id = commercial.current_tenant_id());

            CREATE FUNCTION commercial.delivery_proof_source(
                requested_campaign_id uuid, requested_booking_id uuid)
            RETURNS TABLE (
                buyer_tenant_id uuid, supplier_tenant_id uuid,
                campaign_id uuid, booking_id uuid,
                campaign_owner_user_id uuid, campaign_version bigint,
                flight_start date, flight_end date)
            LANGUAGE sql STABLE SECURITY DEFINER
            SET search_path = pg_catalog, commercial
            AS $delivery_proof_source$
                SELECT campaign.tenant_id, booking.supplier_tenant_id,
                    campaign.id, booking.id, campaign.owner_user_id,
                    campaign.version, booking.flight_start, booking.flight_end
                FROM commercial.campaigns campaign
                JOIN commercial.bookings booking
                  ON booking.buyer_tenant_id = campaign.tenant_id
                 AND booking.proposal_decision_id = campaign.proposal_decision_id
                 AND booking.plan_version_id = campaign.plan_version_id
                WHERE campaign.id = requested_campaign_id
                  AND booking.id = requested_booking_id
                  AND campaign.status_code = 'COMPLETED'
                  AND campaign.proof_requested_by IS NOT NULL
                  AND campaign.proof_requested_at_utc IS NOT NULL
                  AND booking.status_code = 'CONFIRMED'
                  AND booking.supplier_tenant_id = commercial.current_tenant_id();
            $delivery_proof_source$;

            REVOKE ALL ON FUNCTION commercial.delivery_proof_source(uuid, uuid) FROM PUBLIC;
            GRANT EXECUTE ON FUNCTION commercial.delivery_proof_source(uuid, uuid)
                TO advertified_app;

            CREATE FUNCTION commercial.enforce_delivery_proof()
            RETURNS trigger LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, commercial
            AS $delivery_proof$
            DECLARE expected record;
            BEGIN
                IF TG_OP = 'DELETE' THEN
                    RAISE EXCEPTION 'delivery proofs cannot be deleted';
                END IF;
                IF TG_OP = 'INSERT' THEN
                    SELECT campaign.status_code AS campaign_status,
                        campaign.proof_requested_at_utc,
                        booking.status_code AS booking_status,
                        booking.flight_start, booking.flight_end,
                        booking.supplier_tenant_id
                    INTO expected
                    FROM commercial.campaigns campaign
                    JOIN commercial.bookings booking
                      ON booking.buyer_tenant_id = campaign.tenant_id
                     AND booking.proposal_decision_id = campaign.proposal_decision_id
                     AND booking.plan_version_id = campaign.plan_version_id
                    WHERE campaign.tenant_id = NEW.buyer_tenant_id
                      AND campaign.id = NEW.campaign_id
                      AND booking.id = NEW.booking_id;
                    IF NOT FOUND OR expected.campaign_status <> 'COMPLETED'
                       OR expected.proof_requested_at_utc IS NULL
                       OR expected.booking_status <> 'CONFIRMED'
                       OR NEW.supplier_tenant_id <> expected.supplier_tenant_id
                       OR NEW.submitter_tenant_id <> commercial.current_tenant_id()
                       OR NEW.submitted_by <> commercial.current_user_id()
                       OR NEW.status_code <> 'SUBMITTED' OR NEW.version <> 1
                       OR (NEW.captured_at_utc AT TIME ZONE 'UTC')::date
                            NOT BETWEEN expected.flight_start AND expected.flight_end
                       OR NEW.submitted_at_utc < NEW.captured_at_utc
                       OR NEW.updated_at_utc <> NEW.submitted_at_utc
                       OR NEW.protected_object_key <>
                            'protected/' || replace(NEW.buyer_tenant_id::text, '-', '') ||
                            '/campaigns/' || replace(NEW.campaign_id::text, '-', '') ||
                            '/proof/' || replace(NEW.id::text, '-', '') || '/' ||
                            NEW.content_sha256 THEN
                        RAISE EXCEPTION 'delivery proof does not match an eligible booking';
                    END IF;
                    RETURN NEW;
                END IF;
                IF NEW.buyer_tenant_id <> commercial.current_tenant_id()
                   OR OLD.status_code <> 'SUBMITTED'
                   OR NEW.status_code NOT IN ('APPROVED', 'REJECTED')
                   OR NEW.reviewed_by <> commercial.current_user_id()
                   OR NEW.reviewed_by = OLD.submitted_by
                   OR NEW.reviewed_at_utc IS NULL
                   OR NEW.reviewed_at_utc < OLD.submitted_at_utc
                   OR NEW.updated_at_utc <> NEW.reviewed_at_utc
                   OR NEW.version <> OLD.version + 1
                   OR (NEW.id, NEW.buyer_tenant_id, NEW.supplier_tenant_id,
                       NEW.campaign_id, NEW.booking_id,
                       NEW.proof_type_collection_code, NEW.proof_type_code,
                       NEW.file_name, NEW.media_type, NEW.size_bytes,
                       NEW.content_sha256, NEW.signature_validated,
                       NEW.malware_scan_status_collection_code,
                       NEW.malware_scan_status_code, NEW.protected_object_key,
                       NEW.captured_at_utc, NEW.location_description,
                       NEW.latitude, NEW.longitude, NEW.source_reference,
                       NEW.submission_reason, NEW.submitted_by,
                       NEW.submitter_tenant_id, NEW.submitted_at_utc)
                      IS DISTINCT FROM
                      (OLD.id, OLD.buyer_tenant_id, OLD.supplier_tenant_id,
                       OLD.campaign_id, OLD.booking_id,
                       OLD.proof_type_collection_code, OLD.proof_type_code,
                       OLD.file_name, OLD.media_type, OLD.size_bytes,
                       OLD.content_sha256, OLD.signature_validated,
                       OLD.malware_scan_status_collection_code,
                       OLD.malware_scan_status_code, OLD.protected_object_key,
                       OLD.captured_at_utc, OLD.location_description,
                       OLD.latitude, OLD.longitude, OLD.source_reference,
                       OLD.submission_reason, OLD.submitted_by,
                       OLD.submitter_tenant_id, OLD.submitted_at_utc) THEN
                    RAISE EXCEPTION 'delivery proof review is invalid';
                END IF;
                RETURN NEW;
            END;
            $delivery_proof$;

            CREATE TRIGGER protect_delivery_proof
                BEFORE INSERT OR UPDATE OR DELETE ON commercial.delivery_proofs
                FOR EACH ROW EXECUTE FUNCTION commercial.enforce_delivery_proof();

            REVOKE ALL ON FUNCTION commercial.enforce_delivery_proof() FROM PUBLIC;
            GRANT SELECT, INSERT, UPDATE ON commercial.delivery_proofs TO advertified_app;
            """);
}
