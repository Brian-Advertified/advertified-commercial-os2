using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class CampaignDeliveryProof
{
    private static void CreateDeliveryProofTable(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            CREATE TABLE commercial.delivery_proofs (
                id uuid NOT NULL,
                buyer_tenant_id uuid NOT NULL,
                supplier_tenant_id uuid NOT NULL,
                campaign_id uuid NOT NULL,
                booking_id uuid NOT NULL,
                proof_type_collection_code varchar(100) NOT NULL DEFAULT 'deliveryProofTypes',
                proof_type_code varchar(100) NOT NULL,
                file_name varchar(255) NOT NULL,
                media_type varchar(100) NOT NULL,
                size_bytes bigint NOT NULL,
                content_sha256 char(64) NOT NULL,
                signature_validated boolean NOT NULL,
                malware_scan_status_collection_code varchar(100) NOT NULL
                    DEFAULT 'malwareScanStatuses',
                malware_scan_status_code varchar(100) NOT NULL,
                protected_object_key varchar(1000) NOT NULL,
                captured_at_utc timestamptz NOT NULL,
                location_description varchar(500) NOT NULL,
                latitude numeric(9,6),
                longitude numeric(9,6),
                source_reference varchar(500) NOT NULL,
                submission_reason varchar(1000) NOT NULL,
                status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                status_code varchar(100) NOT NULL,
                submitted_by uuid NOT NULL,
                submitter_tenant_id uuid NOT NULL,
                submitted_at_utc timestamptz NOT NULL,
                reviewed_by uuid,
                reviewed_at_utc timestamptz,
                review_reason varchar(1000),
                version bigint NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_delivery_proof_parties_id UNIQUE (
                    buyer_tenant_id, supplier_tenant_id, id),
                CONSTRAINT ux_delivery_proof_exact_content UNIQUE (
                    buyer_tenant_id, booking_id, proof_type_code, content_sha256),
                CONSTRAINT ck_delivery_proof_tenants CHECK (
                    buyer_tenant_id <> supplier_tenant_id
                    AND submitter_tenant_id = supplier_tenant_id),
                CONSTRAINT ck_delivery_proof_type_collection CHECK (
                    proof_type_collection_code = 'deliveryProofTypes'),
                CONSTRAINT ck_delivery_proof_status_collection CHECK (
                    status_collection_code = 'lifecycleStatuses'),
                CONSTRAINT ck_delivery_proof_file CHECK (
                    size_bytes > 0 AND size_bytes <= 26214400
                    AND content_sha256 ~ '^[0-9a-f]{64}$'
                    AND btrim(file_name) <> '' AND btrim(media_type) <> ''
                    AND btrim(location_description) <> ''
                    AND btrim(source_reference) <> ''
                    AND btrim(submission_reason) <> ''),
                CONSTRAINT ck_delivery_proof_media CHECK (
                    (proof_type_code = 'PHOTO'
                        AND media_type IN ('image/png', 'image/jpeg'))
                    OR (proof_type_code IN ('PLAYLOG', 'DELIVERY_REPORT')
                        AND media_type = 'application/pdf')),
                CONSTRAINT ck_delivery_proof_protection CHECK (
                    signature_validated = true
                    AND malware_scan_status_collection_code = 'malwareScanStatuses'
                    AND malware_scan_status_code = 'CLEAN'),
                CONSTRAINT ck_delivery_proof_coordinates CHECK (
                    (latitude IS NULL AND longitude IS NULL)
                    OR (latitude IS NOT NULL AND longitude IS NOT NULL
                        AND latitude BETWEEN -90 AND 90
                        AND longitude BETWEEN -180 AND 180)),
                CONSTRAINT ck_delivery_proof_status_shape CHECK (
                    (status_code = 'SUBMITTED' AND reviewed_by IS NULL
                        AND reviewed_at_utc IS NULL AND review_reason IS NULL
                        AND version = 1)
                    OR (status_code IN ('APPROVED', 'REJECTED')
                        AND reviewed_by IS NOT NULL AND reviewed_at_utc IS NOT NULL
                        AND btrim(COALESCE(review_reason, '')) <> '' AND version = 2)),
                CONSTRAINT fk_delivery_proof_buyer FOREIGN KEY (buyer_tenant_id)
                    REFERENCES commercial.tenants (id),
                CONSTRAINT fk_delivery_proof_supplier FOREIGN KEY (supplier_tenant_id)
                    REFERENCES commercial.tenants (id),
                CONSTRAINT fk_delivery_proof_campaign FOREIGN KEY (
                    buyer_tenant_id, campaign_id)
                    REFERENCES commercial.campaigns (tenant_id, id),
                CONSTRAINT fk_delivery_proof_booking FOREIGN KEY (
                    buyer_tenant_id, supplier_tenant_id, booking_id)
                    REFERENCES commercial.bookings (buyer_tenant_id, supplier_tenant_id, id),
                CONSTRAINT fk_delivery_proof_type FOREIGN KEY (
                    proof_type_collection_code, proof_type_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_delivery_proof_status FOREIGN KEY (
                    status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_delivery_proof_malware_status FOREIGN KEY (
                    malware_scan_status_collection_code, malware_scan_status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_delivery_proof_submitter FOREIGN KEY (submitted_by)
                    REFERENCES commercial.users (id),
                CONSTRAINT fk_delivery_proof_submitter_tenant FOREIGN KEY (submitter_tenant_id)
                    REFERENCES commercial.tenants (id),
                CONSTRAINT fk_delivery_proof_reviewer FOREIGN KEY (reviewed_by)
                    REFERENCES commercial.users (id)
            );

            CREATE INDEX ix_delivery_proof_campaign
                ON commercial.delivery_proofs (
                    buyer_tenant_id, campaign_id, submitted_at_utc, id);
            CREATE INDEX ix_delivery_proof_supplier
                ON commercial.delivery_proofs (
                    supplier_tenant_id, status_code, submitted_at_utc, id);
            """);
}
