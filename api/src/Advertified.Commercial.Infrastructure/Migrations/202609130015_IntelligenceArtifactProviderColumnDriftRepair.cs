using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609130015_IntelligenceArtifactProviderColumnDriftRepair")]
public sealed class IntelligenceArtifactProviderColumnDriftRepair : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(CreateInvocations);
        migrationBuilder.Sql(PreserveProviderUsage);
        migrationBuilder.Sql(ConvergeArtifactSchema);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Intelligence artifact provider-column drift repair is forward-only.");

    private const string CreateInvocations = """
        CREATE TABLE IF NOT EXISTS commercial.intelligence_artifact_invocations (
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
            CONSTRAINT pk_intelligence_artifact_invocations PRIMARY KEY (tenant_id, artifact_id, sequence_no),
            CONSTRAINT fk_intelligence_invocation_artifact FOREIGN KEY (tenant_id, artifact_id)
                REFERENCES commercial.intelligence_artifacts(tenant_id, id) ON DELETE CASCADE,
            CONSTRAINT ck_intelligence_invocation_numbers CHECK (
                sequence_no > 0 AND incremental_cost_minor >= 0 AND input_tokens >= 0 AND
                output_tokens >= 0 AND incremental_cost_usd_micros >= 0),
            CONSTRAINT ck_intelligence_invocation_text CHECK (
                btrim(operation_code) <> '' AND btrim(provider_code) <> '' AND
                btrim(model_code) <> '' AND btrim(cache_status) <> ''));

        CREATE INDEX IF NOT EXISTS ix_intelligence_invocation_model
            ON commercial.intelligence_artifact_invocations (
                tenant_id, provider_code, model_code, operation_code);
        ALTER TABLE commercial.intelligence_artifact_invocations ENABLE ROW LEVEL SECURITY;
        ALTER TABLE commercial.intelligence_artifact_invocations FORCE ROW LEVEL SECURITY;
        DO $$
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM pg_policy
                WHERE polrelid = 'commercial.intelligence_artifact_invocations'::regclass
                  AND polname = 'intelligence_artifact_invocations_tenant_scope') THEN
                CREATE POLICY intelligence_artifact_invocations_tenant_scope
                    ON commercial.intelligence_artifact_invocations
                    USING (tenant_id = commercial.current_tenant_id())
                    WITH CHECK (tenant_id = commercial.current_tenant_id());
            END IF;
        END
        $$;
        GRANT SELECT, INSERT ON TABLE commercial.intelligence_artifact_invocations TO advertified_app;
        """;

    // Backfill is inside the migration transaction. RLS stays ENABLED and FORCED.
    // These migration-role-only policies are created and removed before commit;
    // no application session receives cross-tenant access at any point.
    private const string PreserveProviderUsage = """
        DO $$
        DECLARE
            legacy_columns integer;
        BEGIN
            SELECT count(*) INTO legacy_columns FROM information_schema.columns
            WHERE table_schema = 'commercial' AND table_name = 'intelligence_artifacts'
              AND column_name = ANY(ARRAY['agent_provider_code', 'agent_model_code',
                  'agent_incremental_cost_minor', 'agent_provider_request_id']);
            IF legacy_columns = 0 THEN RETURN; END IF;
            IF legacy_columns <> 4 THEN
                RAISE EXCEPTION 'Incomplete legacy intelligence usage schema; repair aborted without deleting data.';
            END IF;

            LOCK TABLE commercial.intelligence_artifacts IN ACCESS EXCLUSIVE MODE;
            LOCK TABLE commercial.intelligence_artifact_invocations IN ACCESS EXCLUSIVE MODE;
            CREATE POLICY intelligence_provider_repair_read
                ON commercial.intelligence_artifacts FOR SELECT TO advertified_migrator USING (true);
            CREATE POLICY intelligence_provider_repair_write
                ON commercial.intelligence_artifact_invocations TO advertified_migrator
                USING (true) WITH CHECK (true);

            IF EXISTS (
                SELECT 1 FROM commercial.intelligence_artifacts a
                WHERE EXISTS (SELECT 1 FROM commercial.intelligence_artifact_invocations i
                    WHERE i.tenant_id = a.tenant_id AND i.artifact_id = a.id)
                  AND NOT EXISTS (SELECT 1 FROM commercial.intelligence_artifact_invocations i
                    WHERE i.tenant_id = a.tenant_id AND i.artifact_id = a.id
                      AND i.operation_code = a.service_code
                      AND i.provider_code = a.agent_provider_code AND i.model_code = a.agent_model_code
                      AND i.incremental_cost_minor = a.agent_incremental_cost_minor
                      AND i.provider_request_id IS NOT DISTINCT FROM a.agent_provider_request_id)) THEN
                RAISE EXCEPTION 'Conflicting legacy intelligence usage; repair aborted without overwriting evidence.';
            END IF;

            -- LEGACY explicitly identifies missing historical token/micro-cost detail.
            -- Do not reclassify old live usage as a new accepted provider invocation.
            INSERT INTO commercial.intelligence_artifact_invocations (
                tenant_id, artifact_id, sequence_no, operation_code, provider_code,
                model_code, incremental_cost_minor, cache_status, provider_request_id)
            SELECT a.tenant_id, a.id, 1, a.service_code, a.agent_provider_code,
                a.agent_model_code, a.agent_incremental_cost_minor,
                CASE WHEN a.agent_provider_code = 'deterministic' THEN 'FIXTURE' ELSE 'LEGACY' END,
                a.agent_provider_request_id
            FROM commercial.intelligence_artifacts a
            WHERE NOT EXISTS (SELECT 1 FROM commercial.intelligence_artifact_invocations i
                WHERE i.tenant_id = a.tenant_id AND i.artifact_id = a.id);

            DROP POLICY intelligence_provider_repair_write ON commercial.intelligence_artifact_invocations;
            DROP POLICY intelligence_provider_repair_read ON commercial.intelligence_artifacts;
        END
        $$;
        """;

    private const string ConvergeArtifactSchema = """
        -- The old checks reference provider columns. Replace them explicitly so
        -- dropping those columns cannot silently remove the remaining invariants.
        ALTER TABLE commercial.intelligence_artifacts
            DROP CONSTRAINT IF EXISTS ck_intelligence_artifact_numbers,
            DROP CONSTRAINT IF EXISTS ck_intelligence_artifact_text,
            DROP COLUMN IF EXISTS agent_provider_code,
            DROP COLUMN IF EXISTS agent_model_code,
            DROP COLUMN IF EXISTS agent_incremental_cost_minor,
            DROP COLUMN IF EXISTS agent_provider_request_id,
            ADD CONSTRAINT ck_intelligence_artifact_numbers CHECK (
                subject_version > 0 AND version_no > 0 AND version > 0),
            ADD CONSTRAINT ck_intelligence_artifact_text CHECK (
                btrim(subject_type) <> '' AND btrim(service_code) <> '' AND
                btrim(artifact_schema_version) <> '' AND btrim(status_code) <> '');
        """;
}
