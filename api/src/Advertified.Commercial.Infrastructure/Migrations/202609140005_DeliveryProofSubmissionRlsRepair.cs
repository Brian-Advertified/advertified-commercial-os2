using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609140005_DeliveryProofSubmissionRlsRepair")]
public sealed class DeliveryProofSubmissionRlsRepair : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RepairFunction);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Delivery proof submission RLS repair is forward-only.");

    private const string RepairFunction = """
        CREATE OR REPLACE FUNCTION commercial.enforce_delivery_proof() RETURNS trigger
            LANGUAGE plpgsql SECURITY DEFINER
            SET search_path TO 'pg_catalog', 'commercial'
        AS $$
        DECLARE expected record;
        BEGIN
            IF TG_OP = 'DELETE' THEN
                RAISE EXCEPTION 'delivery proofs cannot be deleted';
            END IF;
            IF TG_OP = 'INSERT' THEN
                SELECT request.flight_start, request.flight_end,
                    request.supplier_tenant_id, request.proof_requested_at_utc
                INTO expected
                FROM commercial.delivery_proof_requests request
                WHERE request.buyer_tenant_id = NEW.buyer_tenant_id
                  AND request.supplier_tenant_id = NEW.supplier_tenant_id
                  AND request.campaign_id = NEW.campaign_id
                  AND request.booking_id = NEW.booking_id;
                IF NOT FOUND
                   OR NEW.supplier_tenant_id <> expected.supplier_tenant_id
                   OR NEW.submitter_tenant_id <> commercial.current_tenant_id()
                   OR NEW.submitted_by <> commercial.current_user_id()
                   OR NEW.status_code <> 'SUBMITTED' OR NEW.version <> 1
                   OR (NEW.captured_at_utc AT TIME ZONE 'UTC')::date
                        NOT BETWEEN expected.flight_start AND expected.flight_end
                   OR NEW.submitted_at_utc < expected.proof_requested_at_utc
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
        $$;
        REVOKE ALL ON FUNCTION commercial.enforce_delivery_proof() FROM PUBLIC;
        """;
}
