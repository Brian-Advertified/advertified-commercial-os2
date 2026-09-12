using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609100003_CommercialIntelligenceKernel")]
public sealed class CommercialIntelligenceKernel : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE governance.intelligence_sources (
            id uuid NOT NULL,
            source_key varchar(200) NOT NULL,
            content_hash char(64) NOT NULL,
            title varchar(500) NOT NULL,
            publisher varchar(300) NOT NULL,
            measurement_period varchar(100) NOT NULL,
            source_locator varchar(2000) NOT NULL,
            licence_status varchar(200) NOT NULL,
            promotion_status varchar(100) NOT NULL,
            usage_scope varchar(100) NOT NULL,
            methodology varchar(4000) NOT NULL,
            universe varchar(2000) NOT NULL,
            capabilities_json jsonb NOT NULL DEFAULT '[]'::jsonb,
            metadata_json jsonb NOT NULL DEFAULT '{}'::jsonb,
            quality_notes_json jsonb NOT NULL DEFAULT '[]'::jsonb,
            is_current boolean NOT NULL DEFAULT true,
            loaded_at_utc timestamptz NOT NULL,
            version bigint NOT NULL DEFAULT 1,
            CONSTRAINT pk_intelligence_sources PRIMARY KEY (id),
            CONSTRAINT ux_intelligence_source_version UNIQUE (source_key, content_hash),
            CONSTRAINT ck_intelligence_source_hash CHECK (content_hash ~ '^[0-9a-f]{64}$'),
            CONSTRAINT ck_intelligence_source_json CHECK (
                jsonb_typeof(capabilities_json) = 'array' AND
                jsonb_typeof(metadata_json) = 'object' AND
                jsonb_typeof(quality_notes_json) = 'array'),
            CONSTRAINT ck_intelligence_source_text CHECK (
                btrim(source_key) <> '' AND btrim(title) <> '' AND btrim(publisher) <> '' AND
                btrim(measurement_period) <> '' AND btrim(source_locator) <> '' AND
                btrim(licence_status) <> '' AND btrim(promotion_status) <> '' AND
                btrim(usage_scope) <> '' AND btrim(methodology) <> '' AND btrim(universe) <> ''),
            CONSTRAINT ck_intelligence_source_version CHECK (version > 0));

        CREATE UNIQUE INDEX ux_intelligence_source_current
            ON governance.intelligence_sources (source_key)
            WHERE is_current;

        CREATE TABLE governance.intelligence_observations (
            id uuid NOT NULL,
            source_id uuid NOT NULL,
            source_locator varchar(2000) NOT NULL,
            domain_code varchar(100) NOT NULL,
            geography_level varchar(100),
            geography_code varchar(100),
            geography_name varchar(300),
            dimensions_json jsonb NOT NULL DEFAULT '{}'::jsonb,
            metric_code varchar(150) NOT NULL,
            metric_value numeric(24,6) NOT NULL,
            metric_unit varchar(100) NOT NULL,
            stability_code varchar(100) NOT NULL,
            sensitivity_code varchar(100) NOT NULL,
            activation_policy varchar(100) NOT NULL,
            evidence_notes_json jsonb NOT NULL DEFAULT '[]'::jsonb,
            loaded_at_utc timestamptz NOT NULL,
            CONSTRAINT pk_intelligence_observations PRIMARY KEY (id),
            CONSTRAINT ux_intelligence_observation_locator UNIQUE (source_id, source_locator, metric_code),
            CONSTRAINT fk_intelligence_observation_source FOREIGN KEY (source_id)
                REFERENCES governance.intelligence_sources(id),
            CONSTRAINT ck_intelligence_observation_json CHECK (
                jsonb_typeof(dimensions_json) = 'object' AND
                jsonb_typeof(evidence_notes_json) = 'array'),
            CONSTRAINT ck_intelligence_observation_text CHECK (
                btrim(source_locator) <> '' AND btrim(domain_code) <> '' AND
                btrim(metric_code) <> '' AND btrim(metric_unit) <> '' AND
                btrim(stability_code) <> '' AND btrim(sensitivity_code) <> '' AND
                btrim(activation_policy) <> ''),
            CONSTRAINT ck_intelligence_observation_geography CHECK (
                (geography_level IS NULL AND geography_code IS NULL AND geography_name IS NULL) OR
                (geography_level IS NOT NULL AND geography_code IS NOT NULL AND geography_name IS NOT NULL)));

        CREATE INDEX ix_intelligence_observation_domain
            ON governance.intelligence_observations (domain_code, source_id);
        CREATE INDEX ix_intelligence_observation_geography
            ON governance.intelligence_observations (geography_code, geography_level, domain_code)
            WHERE geography_code IS NOT NULL;
        CREATE INDEX ix_intelligence_observation_dimensions
            ON governance.intelligence_observations USING gin (dimensions_json);
        CREATE INDEX ix_intelligence_observation_activation
            ON governance.intelligence_observations (activation_policy, sensitivity_code, domain_code);

        CREATE TABLE commercial.intelligence_artifacts (
            id uuid NOT NULL,
            tenant_id uuid NOT NULL,
            subject_type varchar(100) NOT NULL,
            subject_id uuid NOT NULL,
            subject_version bigint NOT NULL,
            service_code varchar(100) NOT NULL,
            artifact_schema_version varchar(50) NOT NULL,
            version_no integer NOT NULL,
            artifact_json jsonb NOT NULL,
            unknowns_json jsonb NOT NULL DEFAULT '[]'::jsonb,
            assumptions_json jsonb NOT NULL DEFAULT '[]'::jsonb,
            input_hash char(64) NOT NULL,
            status_code varchar(100) NOT NULL,
            supersedes_artifact_id uuid,
            created_by uuid NOT NULL,
            approved_by uuid,
            created_at_utc timestamptz NOT NULL,
            approved_at_utc timestamptz,
            version bigint NOT NULL DEFAULT 1,
            CONSTRAINT pk_intelligence_artifacts PRIMARY KEY (tenant_id, id),
            CONSTRAINT ux_intelligence_artifact_version UNIQUE (
                tenant_id, subject_type, subject_id, service_code, version_no),
            CONSTRAINT fk_intelligence_artifact_tenant FOREIGN KEY (tenant_id)
                REFERENCES commercial.tenants(id),
            CONSTRAINT fk_intelligence_artifact_supersedes FOREIGN KEY (tenant_id, supersedes_artifact_id)
                REFERENCES commercial.intelligence_artifacts(tenant_id, id),
            CONSTRAINT ck_intelligence_artifact_json CHECK (
                jsonb_typeof(artifact_json) = 'object' AND
                jsonb_typeof(unknowns_json) = 'array' AND
                jsonb_typeof(assumptions_json) = 'array'),
            CONSTRAINT ck_intelligence_artifact_hash CHECK (input_hash ~ '^[0-9a-f]{64}$'),
            CONSTRAINT ck_intelligence_artifact_numbers CHECK (
                subject_version > 0 AND version_no > 0 AND version > 0),
            CONSTRAINT ck_intelligence_artifact_text CHECK (
                btrim(subject_type) <> '' AND btrim(service_code) <> '' AND
                btrim(artifact_schema_version) <> '' AND btrim(status_code) <> ''),
            CONSTRAINT ck_intelligence_artifact_approval CHECK (
                (approved_by IS NULL AND approved_at_utc IS NULL) OR
                (approved_by IS NOT NULL AND approved_at_utc IS NOT NULL)));

        CREATE INDEX ix_intelligence_artifact_subject
            ON commercial.intelligence_artifacts (tenant_id, subject_type, subject_id, service_code, version_no DESC);
        CREATE INDEX ix_intelligence_artifact_status
            ON commercial.intelligence_artifacts (tenant_id, service_code, status_code, created_at_utc DESC);

        CREATE TABLE commercial.intelligence_artifact_invocations (
            tenant_id uuid NOT NULL,
            artifact_id uuid NOT NULL,
            sequence_no integer NOT NULL,
            operation_code varchar(100) NOT NULL,
            provider_code varchar(100) NOT NULL,
            model_code varchar(300) NOT NULL,
            incremental_cost_minor bigint NOT NULL,
            cache_status varchar(100) NOT NULL,
            provider_request_id varchar(500),
            input_tokens bigint NOT NULL DEFAULT 0,
            output_tokens bigint NOT NULL DEFAULT 0,
            incremental_cost_usd_micros bigint NOT NULL DEFAULT 0,
            CONSTRAINT pk_intelligence_artifact_invocations PRIMARY KEY (
                tenant_id, artifact_id, sequence_no),
            CONSTRAINT fk_intelligence_invocation_artifact FOREIGN KEY (tenant_id, artifact_id)
                REFERENCES commercial.intelligence_artifacts(tenant_id, id) ON DELETE CASCADE,
            CONSTRAINT ck_intelligence_invocation_numbers CHECK (
                sequence_no > 0 AND incremental_cost_minor >= 0 AND input_tokens >= 0 AND
                output_tokens >= 0 AND incremental_cost_usd_micros >= 0),
            CONSTRAINT ck_intelligence_invocation_text CHECK (
                btrim(operation_code) <> '' AND btrim(provider_code) <> '' AND
                btrim(model_code) <> '' AND btrim(cache_status) <> ''));

        CREATE INDEX ix_intelligence_invocation_model
            ON commercial.intelligence_artifact_invocations (
                tenant_id, provider_code, model_code, operation_code);

        CREATE TABLE commercial.intelligence_artifact_dependencies (
            tenant_id uuid NOT NULL,
            artifact_id uuid NOT NULL,
            resource_type varchar(100) NOT NULL,
            resource_id uuid NOT NULL,
            resource_version bigint NOT NULL,
            purpose_code varchar(100) NOT NULL,
            CONSTRAINT pk_intelligence_artifact_dependencies PRIMARY KEY (
                tenant_id, artifact_id, resource_type, resource_id, resource_version, purpose_code),
            CONSTRAINT fk_intelligence_dependency_artifact FOREIGN KEY (tenant_id, artifact_id)
                REFERENCES commercial.intelligence_artifacts(tenant_id, id) ON DELETE CASCADE,
            CONSTRAINT ck_intelligence_dependency_version CHECK (resource_version > 0),
            CONSTRAINT ck_intelligence_dependency_text CHECK (
                btrim(resource_type) <> '' AND btrim(purpose_code) <> ''));

        CREATE INDEX ix_intelligence_dependency_resource
            ON commercial.intelligence_artifact_dependencies (
                tenant_id, resource_type, resource_id, resource_version);

        CREATE TABLE commercial.intelligence_artifact_evidence (
            id uuid NOT NULL,
            tenant_id uuid NOT NULL,
            artifact_id uuid NOT NULL,
            field_path varchar(500) NOT NULL,
            classification_code varchar(100) NOT NULL,
            evidence_item_id uuid,
            reference_observation_id uuid,
            rationale varchar(2000),
            CONSTRAINT pk_intelligence_artifact_evidence PRIMARY KEY (tenant_id, id),
            CONSTRAINT fk_intelligence_evidence_artifact FOREIGN KEY (tenant_id, artifact_id)
                REFERENCES commercial.intelligence_artifacts(tenant_id, id) ON DELETE CASCADE,
            CONSTRAINT fk_intelligence_evidence_item FOREIGN KEY (tenant_id, evidence_item_id)
                REFERENCES commercial.evidence_items(tenant_id, id),
            CONSTRAINT fk_intelligence_evidence_reference FOREIGN KEY (reference_observation_id)
                REFERENCES governance.intelligence_observations(id),
            CONSTRAINT ck_intelligence_evidence_pointer CHECK (
                (evidence_item_id IS NOT NULL AND reference_observation_id IS NULL) OR
                (evidence_item_id IS NULL AND reference_observation_id IS NOT NULL)),
            CONSTRAINT ck_intelligence_evidence_text CHECK (
                btrim(field_path) <> '' AND btrim(classification_code) <> ''));

        CREATE UNIQUE INDEX ux_intelligence_evidence_item_binding
            ON commercial.intelligence_artifact_evidence (
                tenant_id, artifact_id, field_path, classification_code, evidence_item_id)
            WHERE evidence_item_id IS NOT NULL;
        CREATE UNIQUE INDEX ux_intelligence_reference_binding
            ON commercial.intelligence_artifact_evidence (
                tenant_id, artifact_id, field_path, classification_code, reference_observation_id)
            WHERE reference_observation_id IS NOT NULL;

        -- Migrate the pre-kernel audience model into the canonical Intelligence Artifact model.
        -- IDs are preserved so downstream media/email records can be retargeted without copies.
        INSERT INTO commercial.intelligence_artifacts (
            id, tenant_id, subject_type, subject_id, subject_version,
            service_code, artifact_schema_version, version_no, artifact_json,
            unknowns_json, assumptions_json, input_hash,
            status_code, supersedes_artifact_id,
            created_by, approved_by, created_at_utc, approved_at_utc, version)
        SELECT audience.id,
            audience.tenant_id,
            'BriefVersion',
            audience.brief_version_id,
            brief.version,
            'audience_intelligence',
            'audience-strategy.v1',
            audience.version_no,
            jsonb_build_object(
                'targetAudienceIds', audience.target_audience_ids_json,
                'targetingRationale', audience.targeting_rationale,
                'positioningStatement', audience.positioning_statement,
                'segments', COALESCE((
                    SELECT jsonb_agg(jsonb_build_object(
                        'id', definition.id,
                        'name', definition.name,
                        'description', definition.description,
                        'needState', definition.need_state,
                        'buyingContext', definition.buying_context,
                        'geographies', definition.geography_json,
                        'language', definition.language,
                        'lifeStage', definition.life_stage,
                        'lsmSem', definition.lsm_sem,
                        'lsmSemTaxonomy', definition.lsm_sem_taxonomy,
                        'lsmSemTaxonomyVersion', definition.lsm_sem_taxonomy_version,
                        'classification', definition.classification_code,
                        'exclusions', definition.exclusions_json,
                        'evidenceItemIds', definition.evidence_item_ids_json,
                        'referenceObservationIds', '[]'::jsonb,
                        'confidence', definition.confidence,
                        'lsmSemMandatory', definition.lsm_sem_mandatory)
                        ORDER BY definition.name, definition.id)
                    FROM commercial.audience_definitions definition
                    WHERE definition.tenant_id = audience.tenant_id
                      AND definition.audience_set_id = audience.id), '[]'::jsonb)),
            '[]'::jsonb,
            '[]'::jsonb,
            audience.input_hash,
            audience.status_code,
            NULL,
            audience.created_by,
            audience.approved_by,
            audience.created_at_utc,
            audience.approved_at_utc,
            audience.version
        FROM commercial.audience_definition_sets audience
        JOIN commercial.brief_versions brief
          ON brief.tenant_id = audience.tenant_id
         AND brief.id = audience.brief_version_id;

        INSERT INTO commercial.intelligence_artifact_invocations (
            tenant_id, artifact_id, sequence_no, operation_code,
            provider_code, model_code, incremental_cost_minor, cache_status,
            provider_request_id, input_tokens, output_tokens, incremental_cost_usd_micros)
        SELECT audience.tenant_id, audience.id, 1, 'audience_intelligence',
            COALESCE(audience.agent_provider_code, 'legacy'),
            COALESCE(audience.agent_model_code, 'legacy-unattributed'),
            COALESCE(audience.agent_incremental_cost_minor, 0),
            CASE WHEN audience.agent_provider_code = 'deterministic' THEN 'FIXTURE' ELSE 'LEGACY' END,
            audience.agent_provider_request_id, 0, 0, 0
        FROM commercial.audience_definition_sets audience;

        WITH lineage AS (
            SELECT tenant_id, id,
                lag(id) OVER (
                    PARTITION BY tenant_id, subject_id, service_code
                    ORDER BY version_no) AS previous_id
            FROM commercial.intelligence_artifacts
            WHERE service_code = 'audience_intelligence'
        )
        UPDATE commercial.intelligence_artifacts artifact
        SET supersedes_artifact_id = lineage.previous_id
        FROM lineage
        WHERE artifact.tenant_id = lineage.tenant_id
          AND artifact.id = lineage.id
          AND lineage.previous_id IS NOT NULL;

        INSERT INTO commercial.intelligence_artifact_dependencies (
            tenant_id, artifact_id, resource_type, resource_id, resource_version, purpose_code)
        SELECT tenant_id, id, 'BriefVersion', subject_id, subject_version, 'commercial_problem'
        FROM commercial.intelligence_artifacts
        WHERE service_code = 'audience_intelligence';

        INSERT INTO commercial.intelligence_artifact_evidence (
            id, tenant_id, artifact_id, field_path, classification_code,
            evidence_item_id, reference_observation_id, rationale)
        SELECT gen_random_uuid(), definition.tenant_id, definition.audience_set_id,
            'artifact.segments.' || replace(definition.id::text, '-', ''),
            definition.classification_code,
            evidence.value::uuid,
            NULL,
            'Migrated approved Brief evidence for Audience Intelligence.'
        FROM commercial.audience_definitions definition
        CROSS JOIN LATERAL jsonb_array_elements_text(
            COALESCE(definition.evidence_item_ids_json, '[]'::jsonb)) evidence(value);

        ALTER TABLE commercial.media_mix_versions
            DROP CONSTRAINT fk_media_mix_audience;
        ALTER TABLE commercial.media_mix_versions
            RENAME COLUMN audience_set_id TO audience_artifact_id;
        ALTER TABLE commercial.media_mix_versions
            ADD CONSTRAINT fk_media_mix_audience_artifact
                FOREIGN KEY (tenant_id, audience_artifact_id)
                REFERENCES commercial.intelligence_artifacts(tenant_id, id);
        ALTER TABLE commercial.media_mix_versions
            ADD COLUMN media_strategy_artifact_id uuid NULL;
        ALTER TABLE commercial.media_mix_versions
            ADD CONSTRAINT fk_media_mix_media_strategy_artifact
                FOREIGN KEY (tenant_id, media_strategy_artifact_id)
                REFERENCES commercial.intelligence_artifacts(tenant_id, id);
        ALTER TABLE commercial.media_mix_versions
            DROP CONSTRAINT ck_media_mix_agent_usage,
            DROP COLUMN agent_provider_code,
            DROP COLUMN agent_model_code,
            DROP COLUMN agent_incremental_cost_minor,
            DROP COLUMN agent_provider_request_id;

        ALTER TABLE commercial.email_proposal_automation_runs
            DROP CONSTRAINT fk_email_automation_stp;
        ALTER TABLE commercial.email_proposal_automation_runs
            RENAME COLUMN stp_version_id TO audience_artifact_id;
        ALTER TABLE commercial.email_proposal_automation_runs
            ADD CONSTRAINT fk_email_automation_audience_artifact
                FOREIGN KEY (tenant_id, audience_artifact_id)
                REFERENCES commercial.intelligence_artifacts(tenant_id, id);

        DROP TABLE commercial.audience_definitions;
        DROP TABLE commercial.audience_definition_sets;

        ALTER TABLE commercial.intelligence_artifacts ENABLE ROW LEVEL SECURITY;
        ALTER TABLE commercial.intelligence_artifacts FORCE ROW LEVEL SECURITY;
        CREATE POLICY intelligence_artifacts_tenant_scope
            ON commercial.intelligence_artifacts
            USING (tenant_id = commercial.current_tenant_id())
            WITH CHECK (tenant_id = commercial.current_tenant_id());

        ALTER TABLE commercial.intelligence_artifact_invocations ENABLE ROW LEVEL SECURITY;
        ALTER TABLE commercial.intelligence_artifact_invocations FORCE ROW LEVEL SECURITY;
        CREATE POLICY intelligence_artifact_invocations_tenant_scope
            ON commercial.intelligence_artifact_invocations
            USING (tenant_id = commercial.current_tenant_id())
            WITH CHECK (tenant_id = commercial.current_tenant_id());

        ALTER TABLE commercial.intelligence_artifact_dependencies ENABLE ROW LEVEL SECURITY;
        ALTER TABLE commercial.intelligence_artifact_dependencies FORCE ROW LEVEL SECURITY;
        CREATE POLICY intelligence_artifact_dependencies_tenant_scope
            ON commercial.intelligence_artifact_dependencies
            USING (tenant_id = commercial.current_tenant_id())
            WITH CHECK (tenant_id = commercial.current_tenant_id());

        ALTER TABLE commercial.intelligence_artifact_evidence ENABLE ROW LEVEL SECURITY;
        ALTER TABLE commercial.intelligence_artifact_evidence FORCE ROW LEVEL SECURITY;
        CREATE POLICY intelligence_artifact_evidence_tenant_scope
            ON commercial.intelligence_artifact_evidence
            USING (tenant_id = commercial.current_tenant_id())
            WITH CHECK (tenant_id = commercial.current_tenant_id());

        GRANT SELECT ON TABLE governance.intelligence_sources TO advertified_app;
        GRANT SELECT ON TABLE governance.intelligence_observations TO advertified_app;
        GRANT SELECT, INSERT, UPDATE ON TABLE commercial.intelligence_artifacts TO advertified_app;
        GRANT SELECT, INSERT ON TABLE commercial.intelligence_artifact_invocations TO advertified_app;
        GRANT SELECT, INSERT ON TABLE commercial.intelligence_artifact_dependencies TO advertified_app;
        GRANT SELECT, INSERT ON TABLE commercial.intelligence_artifact_evidence TO advertified_app;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("The commercial intelligence kernel is forward-only.");
}
