using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class CanonicalPlanning
{
    private static void CreateAudienceAndMixTables(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE commercial.audience_definition_sets (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                brief_version_id uuid NOT NULL,
                version_no integer NOT NULL,
                input_hash char(64) NOT NULL,
                status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                status_code varchar(100) NOT NULL,
                created_by uuid NOT NULL,
                created_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_audience_sets_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_audience_sets_version UNIQUE (tenant_id, brief_version_id, version_no),
                CONSTRAINT ck_audience_sets_numbers CHECK (version_no > 0),
                CONSTRAINT ck_audience_sets_hash CHECK (input_hash ~ '^[0-9a-f]{64}$'),
                CONSTRAINT ck_audience_sets_status_collection CHECK (
                    status_collection_code = 'lifecycleStatuses'),
                CONSTRAINT fk_audience_sets_brief FOREIGN KEY (tenant_id, brief_version_id)
                    REFERENCES commercial.brief_versions (tenant_id, id),
                CONSTRAINT fk_audience_sets_status FOREIGN KEY (
                    status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_audience_sets_creator FOREIGN KEY (created_by)
                    REFERENCES commercial.users (id)
            );

            CREATE TABLE commercial.audience_definitions (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                audience_set_id uuid NOT NULL,
                name varchar(300) NOT NULL,
                description text NOT NULL,
                need_state text NOT NULL,
                buying_context text NOT NULL,
                geography_json jsonb NOT NULL,
                language varchar(100),
                life_stage varchar(200),
                lsm_sem varchar(100),
                classification_collection_code varchar(100) NOT NULL
                    DEFAULT 'evidenceClassifications',
                classification_code varchar(100) NOT NULL,
                exclusions_json jsonb NOT NULL,
                evidence_item_ids_json jsonb NOT NULL,
                confidence numeric(5,4) NOT NULL,
                status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                status_code varchar(100) NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_audience_definitions_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ck_audience_confidence CHECK (confidence BETWEEN 0 AND 1),
                CONSTRAINT ck_audience_classification_collection CHECK (
                    classification_collection_code = 'evidenceClassifications'),
                CONSTRAINT ck_audience_status_collection CHECK (
                    status_collection_code = 'lifecycleStatuses'),
                CONSTRAINT fk_audience_definition_set FOREIGN KEY (tenant_id, audience_set_id)
                    REFERENCES commercial.audience_definition_sets (tenant_id, id),
                CONSTRAINT fk_audience_classification FOREIGN KEY (
                    classification_collection_code, classification_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_audience_status FOREIGN KEY (
                    status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code)
            );

            CREATE TABLE commercial.media_mix_versions (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                brief_version_id uuid NOT NULL,
                audience_set_id uuid NOT NULL,
                version_no integer NOT NULL,
                total_budget_minor bigint NOT NULL,
                currency_collection_code varchar(100) NOT NULL DEFAULT 'currencies',
                currency_code varchar(100) NOT NULL,
                allocations_json jsonb NOT NULL,
                channel_roles_json jsonb NOT NULL,
                assumptions_json jsonb NOT NULL,
                evidence_item_ids_json jsonb NOT NULL,
                input_hash char(64) NOT NULL,
                status_collection_code varchar(100) NOT NULL DEFAULT 'lifecycleStatuses',
                status_code varchar(100) NOT NULL,
                created_by uuid NOT NULL,
                approved_by uuid,
                approved_at_utc timestamptz,
                version bigint NOT NULL,
                created_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_media_mix_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_media_mix_version UNIQUE (tenant_id, brief_version_id, version_no),
                CONSTRAINT ck_media_mix_numbers CHECK (
                    version_no > 0 AND total_budget_minor >= 0 AND version > 0),
                CONSTRAINT ck_media_mix_hash CHECK (input_hash ~ '^[0-9a-f]{64}$'),
                CONSTRAINT ck_media_mix_currency_collection CHECK (
                    currency_collection_code = 'currencies'),
                CONSTRAINT ck_media_mix_status_collection CHECK (
                    status_collection_code = 'lifecycleStatuses'),
                CONSTRAINT fk_media_mix_brief FOREIGN KEY (tenant_id, brief_version_id)
                    REFERENCES commercial.brief_versions (tenant_id, id),
                CONSTRAINT fk_media_mix_audience FOREIGN KEY (tenant_id, audience_set_id)
                    REFERENCES commercial.audience_definition_sets (tenant_id, id),
                CONSTRAINT fk_media_mix_currency FOREIGN KEY (
                    currency_collection_code, currency_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_media_mix_status FOREIGN KEY (
                    status_collection_code, status_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_media_mix_creator FOREIGN KEY (created_by)
                    REFERENCES commercial.users (id),
                CONSTRAINT fk_media_mix_approver FOREIGN KEY (approved_by)
                    REFERENCES commercial.users (id)
            );
            """);
    }
}
