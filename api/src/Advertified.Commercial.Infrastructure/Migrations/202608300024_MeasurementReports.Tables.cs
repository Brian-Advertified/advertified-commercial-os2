using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class MeasurementReports
{
    private static void CreateMeasurementReportTables(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.agent_runs ADD COLUMN campaign_id uuid;
            ALTER TABLE commercial.agent_runs ALTER COLUMN opportunity_id DROP NOT NULL;
            ALTER TABLE commercial.agent_runs ADD CONSTRAINT fk_agent_runs_campaign
                FOREIGN KEY (tenant_id, campaign_id)
                REFERENCES commercial.campaigns (tenant_id, id);
            CREATE INDEX ix_agent_runs_campaign
                ON commercial.agent_runs (tenant_id, campaign_id, created_at_utc, id)
                WHERE campaign_id IS NOT NULL;
            ALTER TABLE commercial.agent_runs ADD CONSTRAINT ck_agent_runs_work_scope CHECK (
                opportunity_id IS NOT NULL OR campaign_id IS NOT NULL);

            CREATE TABLE commercial.measurement_report_versions (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                campaign_id uuid NOT NULL,
                version_no integer NOT NULL,
                agent_run_id uuid NOT NULL,
                campaign_version bigint NOT NULL,
                measurement_plan_json jsonb NOT NULL,
                evidence_versions_json jsonb NOT NULL,
                metric_ids uuid[] NOT NULL,
                interpretation_json jsonb NOT NULL,
                limitations_json jsonb NOT NULL,
                input_hash char(64) NOT NULL,
                output_hash char(64) NOT NULL,
                agent_contract_version varchar(50) NOT NULL,
                prompt_version varchar(50) NOT NULL,
                provider_code varchar(100) NOT NULL,
                model_code varchar(100) NOT NULL,
                tool_calls integer NOT NULL,
                incremental_cost_minor bigint NOT NULL,
                output_validated boolean NOT NULL,
                status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                status_code varchar(100) NOT NULL,
                approver_user_id uuid NOT NULL,
                generated_by uuid NOT NULL,
                generated_at_utc timestamptz NOT NULL,
                reviewed_by uuid,
                reviewed_at_utc timestamptz,
                review_reason varchar(1000),
                version bigint NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_measurement_report_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_measurement_report_campaign_version UNIQUE (
                    tenant_id, campaign_id, version_no),
                CONSTRAINT ux_measurement_report_agent_run UNIQUE (tenant_id, agent_run_id),
                CONSTRAINT ck_measurement_report_versions CHECK (
                    version_no > 0 AND campaign_version > 0 AND version > 0),
                CONSTRAINT ck_measurement_report_sources CHECK (
                    jsonb_typeof(measurement_plan_json) = 'array'
                    AND jsonb_array_length(measurement_plan_json) > 0
                    AND jsonb_typeof(evidence_versions_json) = 'array'
                    AND jsonb_array_length(evidence_versions_json) > 0
                    AND cardinality(metric_ids) > 0
                    AND jsonb_typeof(limitations_json) = 'array'
                    AND jsonb_array_length(limitations_json) > 0),
                CONSTRAINT ck_measurement_report_hashes CHECK (
                    input_hash ~ '^[0-9a-f]{64}$' AND output_hash ~ '^[0-9a-f]{64}$'),
                CONSTRAINT ck_measurement_report_agent CHECK (
                    agent_contract_version = '1.0.0' AND prompt_version = '1.0.0'
                    AND provider_code = 'deterministic' AND model_code = 'fixture-v1'
                    AND tool_calls = 0 AND incremental_cost_minor = 0
                    AND output_validated = true),
                CONSTRAINT ck_measurement_report_status_collection CHECK (
                    status_collection_code = 'lifecycleStatuses'),
                CONSTRAINT ck_measurement_report_status_shape CHECK (
                    (status_code = 'REVIEW_REQUIRED' AND reviewed_by IS NULL
                        AND reviewed_at_utc IS NULL AND review_reason IS NULL AND version = 1)
                    OR (status_code IN ('APPROVED', 'REJECTED')
                        AND reviewed_by IS NOT NULL AND reviewed_at_utc IS NOT NULL
                        AND btrim(COALESCE(review_reason, '')) <> '' AND version = 2)),
                CONSTRAINT fk_measurement_report_campaign FOREIGN KEY (tenant_id, campaign_id)
                    REFERENCES commercial.campaigns (tenant_id, id),
                CONSTRAINT fk_measurement_report_run FOREIGN KEY (tenant_id, agent_run_id)
                    REFERENCES commercial.agent_runs (tenant_id, id),
                CONSTRAINT fk_measurement_report_status FOREIGN KEY (
                    status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_measurement_report_approver FOREIGN KEY (approver_user_id)
                    REFERENCES commercial.users (id),
                CONSTRAINT fk_measurement_report_generator FOREIGN KEY (generated_by)
                    REFERENCES commercial.users (id),
                CONSTRAINT fk_measurement_report_reviewer FOREIGN KEY (reviewed_by)
                    REFERENCES commercial.users (id)
            );

            CREATE INDEX ix_measurement_report_campaign
                ON commercial.measurement_report_versions (
                    tenant_id, campaign_id, status_code, version_no, id);
            """);
}
