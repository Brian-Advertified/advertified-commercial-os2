using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class PerformanceEvidenceFacts
{
    private static void CreatePerformanceEvidenceTables(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            CREATE TABLE commercial.performance_evidence_sets (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                campaign_id uuid NOT NULL,
                source_reference varchar(500) NOT NULL,
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
                methodology varchar(2000) NOT NULL,
                limitations_json jsonb NOT NULL,
                quality_status_collection_code varchar(100) NOT NULL
                    DEFAULT 'measurementQualityStatuses',
                quality_status_code varchar(100) NOT NULL,
                status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                status_code varchar(100) NOT NULL,
                reviewer_user_id uuid NOT NULL,
                created_by uuid NOT NULL,
                created_at_utc timestamptz NOT NULL,
                submitted_by uuid,
                submitted_at_utc timestamptz,
                reviewed_by uuid,
                reviewed_at_utc timestamptz,
                review_reason varchar(1000),
                version bigint NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_performance_evidence_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_performance_evidence_exact_content UNIQUE (
                    tenant_id, campaign_id, content_sha256),
                CONSTRAINT ck_performance_evidence_file CHECK (
                    size_bytes > 0 AND size_bytes <= 26214400
                    AND content_sha256 ~ '^[0-9a-f]{64}$'
                    AND btrim(source_reference) <> '' AND btrim(file_name) <> ''
                    AND btrim(media_type) <> '' AND btrim(methodology) <> ''),
                CONSTRAINT ck_performance_evidence_protection CHECK (
                    signature_validated = true
                    AND malware_scan_status_collection_code = 'malwareScanStatuses'
                    AND malware_scan_status_code = 'CLEAN'),
                CONSTRAINT ck_performance_evidence_limitations CHECK (
                    jsonb_typeof(limitations_json) = 'array'
                    AND jsonb_array_length(limitations_json) > 0),
                CONSTRAINT ck_performance_evidence_quality_collection CHECK (
                    quality_status_collection_code = 'measurementQualityStatuses'),
                CONSTRAINT ck_performance_evidence_status_collection CHECK (
                    status_collection_code = 'lifecycleStatuses'),
                CONSTRAINT ck_performance_evidence_status_shape CHECK (
                    (status_code = 'DRAFT' AND submitted_by IS NULL
                        AND submitted_at_utc IS NULL AND reviewed_by IS NULL
                        AND reviewed_at_utc IS NULL AND review_reason IS NULL AND version = 0)
                    OR (status_code = 'SUBMITTED' AND submitted_by IS NOT NULL
                        AND submitted_at_utc IS NOT NULL AND reviewed_by IS NULL
                        AND reviewed_at_utc IS NULL AND review_reason IS NULL AND version = 1)
                    OR (status_code IN ('APPROVED', 'REJECTED')
                        AND submitted_by IS NOT NULL AND submitted_at_utc IS NOT NULL
                        AND reviewed_by IS NOT NULL AND reviewed_at_utc IS NOT NULL
                        AND btrim(COALESCE(review_reason, '')) <> '' AND version = 2)),
                CONSTRAINT fk_performance_evidence_tenant FOREIGN KEY (tenant_id)
                    REFERENCES commercial.tenants (id),
                CONSTRAINT fk_performance_evidence_campaign FOREIGN KEY (tenant_id, campaign_id)
                    REFERENCES commercial.campaigns (tenant_id, id),
                CONSTRAINT fk_performance_evidence_malware FOREIGN KEY (
                    malware_scan_status_collection_code, malware_scan_status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_performance_evidence_quality FOREIGN KEY (
                    quality_status_collection_code, quality_status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_performance_evidence_status FOREIGN KEY (
                    status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_performance_evidence_reviewer FOREIGN KEY (reviewer_user_id)
                    REFERENCES commercial.users (id),
                CONSTRAINT fk_performance_evidence_creator FOREIGN KEY (created_by)
                    REFERENCES commercial.users (id),
                CONSTRAINT fk_performance_evidence_submitter FOREIGN KEY (submitted_by)
                    REFERENCES commercial.users (id),
                CONSTRAINT fk_performance_evidence_review_actor FOREIGN KEY (reviewed_by)
                    REFERENCES commercial.users (id)
            );

            CREATE TABLE commercial.performance_metrics (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                campaign_id uuid NOT NULL,
                evidence_set_id uuid NOT NULL,
                metric_type_collection_code varchar(100) NOT NULL
                    DEFAULT 'performanceMetricTypes',
                metric_type_code varchar(100) NOT NULL,
                value numeric(20,6) NOT NULL,
                unit_collection_code varchar(100) NOT NULL DEFAULT 'measurementUnits',
                unit_code varchar(100) NOT NULL,
                period_start date NOT NULL,
                period_end date NOT NULL,
                source_locator varchar(500) NOT NULL,
                created_by uuid NOT NULL,
                created_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_performance_metric_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_performance_metric_exact_fact UNIQUE (
                    tenant_id, evidence_set_id, metric_type_code, unit_code,
                    period_start, period_end),
                CONSTRAINT ck_performance_metric_collections CHECK (
                    metric_type_collection_code = 'performanceMetricTypes'
                    AND unit_collection_code = 'measurementUnits'),
                CONSTRAINT ck_performance_metric_value CHECK (
                    value >= 0 AND period_end >= period_start
                    AND btrim(source_locator) <> ''
                    AND (unit_code <> 'PERCENT' OR value <= 100)),
                CONSTRAINT ck_performance_metric_unit CHECK (
                    (metric_type_code IN ('REACH', 'FOOTFALL') AND unit_code = 'PEOPLE')
                    OR (metric_type_code IN ('CLICK_THROUGH_RATE', 'CONVERSION_RATE')
                        AND unit_code = 'PERCENT')
                    OR (metric_type_code IN (
                        'IMPRESSIONS', 'CLICKS', 'CONVERSIONS') AND unit_code = 'COUNT')),
                CONSTRAINT fk_performance_metric_campaign FOREIGN KEY (tenant_id, campaign_id)
                    REFERENCES commercial.campaigns (tenant_id, id),
                CONSTRAINT fk_performance_metric_evidence FOREIGN KEY (
                    tenant_id, evidence_set_id)
                    REFERENCES commercial.performance_evidence_sets (tenant_id, id),
                CONSTRAINT fk_performance_metric_type FOREIGN KEY (
                    metric_type_collection_code, metric_type_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_performance_metric_unit FOREIGN KEY (
                    unit_collection_code, unit_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_performance_metric_creator FOREIGN KEY (created_by)
                    REFERENCES commercial.users (id)
            );

            CREATE INDEX ix_performance_evidence_campaign
                ON commercial.performance_evidence_sets (
                    tenant_id, campaign_id, status_code, submitted_at_utc, id);
            CREATE INDEX ix_performance_metric_campaign
                ON commercial.performance_metrics (
                    tenant_id, campaign_id, metric_type_code, period_start, id);
            """);
}
