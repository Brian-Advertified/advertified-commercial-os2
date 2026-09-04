using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609030049_InventoryExtractionReprojection")]
public sealed class InventoryExtractionReprojection : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE commercial.inventory_extraction_projections (
                id uuid NOT NULL PRIMARY KEY,
                tenant_id uuid NOT NULL,
                import_id uuid NOT NULL,
                input_artifact_id uuid NOT NULL,
                attempt_id uuid,
                projector_code varchar(100) NOT NULL,
                projector_version varchar(300) NOT NULL,
                schema_version varchar(200) NOT NULL,
                canonical_json jsonb,
                canonical_output_hash char(64) NOT NULL,
                candidate_count integer NOT NULL,
                created_by uuid NOT NULL,
                created_at_utc timestamptz NOT NULL,
                CONSTRAINT ux_inventory_projection_tenant_id
                    UNIQUE (tenant_id, id),
                CONSTRAINT ux_inventory_projection_attempt
                    UNIQUE (tenant_id, attempt_id),
                CONSTRAINT ux_inventory_projection_output
                    UNIQUE (
                        tenant_id, input_artifact_id,
                        canonical_output_hash),
                CONSTRAINT ck_inventory_projection_hash CHECK (
                    canonical_output_hash ~ '^[0-9a-f]{64}$'),
                CONSTRAINT ck_inventory_projection_count CHECK (
                    candidate_count >= 0),
                CONSTRAINT ck_inventory_projection_identity CHECK (
                    btrim(projector_code) <> ''
                    AND btrim(projector_version) <> ''
                    AND btrim(schema_version) <> ''),
                CONSTRAINT ck_inventory_projection_payload CHECK (
                    canonical_json IS NULL
                    OR jsonb_typeof(canonical_json) = 'object'),
                CONSTRAINT fk_inventory_projection_import
                    FOREIGN KEY (tenant_id, import_id)
                    REFERENCES commercial.inventory_imports (
                        tenant_id, id),
                CONSTRAINT fk_inventory_projection_input
                    FOREIGN KEY (tenant_id, input_artifact_id)
                    REFERENCES commercial.inventory_extractions (
                        tenant_id, id),
                CONSTRAINT fk_inventory_projection_attempt
                    FOREIGN KEY (tenant_id, attempt_id)
                    REFERENCES
                        commercial.inventory_extraction_attempts (
                            tenant_id, id),
                CONSTRAINT fk_inventory_projection_creator
                    FOREIGN KEY (created_by)
                    REFERENCES commercial.users (id)
            );

            ALTER TABLE commercial.inventory_imports
                NO FORCE ROW LEVEL SECURITY;
            ALTER TABLE commercial.inventory_extractions
                NO FORCE ROW LEVEL SECURITY;
            ALTER TABLE commercial.inventory_candidates
                NO FORCE ROW LEVEL SECURITY;

            INSERT INTO commercial.inventory_extractions (
                id, tenant_id, import_id, source_hash,
                adapter_code, adapter_version, schema_version,
                provider_json, provider_output_hash,
                completed_at_utc, canonical_json,
                canonical_output_hash, attempt_id,
                source_file_version)
            SELECT gen_random_uuid(), source.tenant_id, source.id,
                source.source_hash, 'legacy-candidate-backfill',
                '1.0.0', 'legacy-candidate-backfill-v1',
                payload.provider_json,
                encode(digest(
                    payload.provider_json::text, 'sha256'), 'hex'),
                source.updated_at_utc, payload.canonical_json,
                encode(digest(
                    payload.canonical_json::text, 'sha256'), 'hex'),
                NULL, source.version
            FROM commercial.inventory_imports source
            CROSS JOIN LATERAL (
                SELECT
                    jsonb_build_object(
                        'origin', 'pre-projection-candidate-state',
                        'sourceHash', source.source_hash)
                        AS provider_json,
                    jsonb_build_object(
                        'schemaVersion',
                            'legacy-candidate-backfill-v1',
                        'rows', jsonb_agg(
                            jsonb_build_object(
                                'number', candidate.row_number,
                                'locator', candidate.source_locator,
                                'values',
                                    candidate.proposed_values_json)
                            ORDER BY candidate.row_number,
                                candidate.id))
                        AS canonical_json
                FROM commercial.inventory_candidates candidate
                WHERE candidate.tenant_id = source.tenant_id
                  AND candidate.import_id = source.id
            ) payload
            WHERE EXISTS (
                  SELECT 1
                  FROM commercial.inventory_candidates candidate
                  WHERE candidate.tenant_id = source.tenant_id
                    AND candidate.import_id = source.id)
              AND NOT EXISTS (
                  SELECT 1
                  FROM commercial.inventory_extractions extraction
                  WHERE extraction.tenant_id = source.tenant_id
                    AND extraction.import_id = source.id);

            INSERT INTO
                commercial.inventory_extraction_projections (
                    id, tenant_id, import_id, input_artifact_id,
                    attempt_id, projector_code, projector_version,
                    schema_version, canonical_json,
                    canonical_output_hash, candidate_count,
                    created_by, created_at_utc)
            SELECT extraction.id, extraction.tenant_id,
                extraction.import_id, extraction.id,
                extraction.attempt_id, extraction.adapter_code,
                extraction.adapter_version,
                extraction.schema_version, NULL,
                extraction.canonical_output_hash,
                (
                    SELECT count(*)::integer
                    FROM commercial.inventory_candidates candidate
                    WHERE candidate.tenant_id =
                            extraction.tenant_id
                      AND candidate.import_id =
                            extraction.import_id
                ),
                source.created_by,
                extraction.completed_at_utc
            FROM commercial.inventory_extractions extraction
            JOIN commercial.inventory_imports source
              ON source.tenant_id = extraction.tenant_id
             AND source.id = extraction.import_id;

            ALTER TABLE
                commercial.inventory_extraction_attempts
                ADD COLUMN input_artifact_id uuid,
                ADD CONSTRAINT
                    fk_inventory_extraction_attempt_input
                    FOREIGN KEY (
                        tenant_id, input_artifact_id)
                    REFERENCES
                        commercial.inventory_extractions (
                            tenant_id, id);

            ALTER TABLE commercial.inventory_candidates
                ADD COLUMN projection_id uuid,
                ADD COLUMN superseded_at_utc timestamptz;

            UPDATE commercial.inventory_candidates candidate
            SET projection_id = (
                SELECT extraction.id
                FROM commercial.inventory_extractions extraction
                WHERE extraction.tenant_id = candidate.tenant_id
                  AND extraction.import_id = candidate.import_id
                ORDER BY extraction.completed_at_utc DESC,
                    extraction.id DESC
                LIMIT 1);

            ALTER TABLE commercial.inventory_imports
                FORCE ROW LEVEL SECURITY;
            ALTER TABLE commercial.inventory_extractions
                FORCE ROW LEVEL SECURITY;
            ALTER TABLE commercial.inventory_candidates
                FORCE ROW LEVEL SECURITY;

            ALTER TABLE commercial.inventory_candidates
                ALTER COLUMN projection_id SET NOT NULL,
                ADD CONSTRAINT
                    fk_inventory_candidate_projection
                    FOREIGN KEY (tenant_id, projection_id)
                    REFERENCES
                        commercial.inventory_extraction_projections (
                            tenant_id, id),
                DROP CONSTRAINT
                    ux_inventory_candidates_import_row;

            CREATE UNIQUE INDEX
                ux_inventory_candidates_current_import_row
                ON commercial.inventory_candidates (
                    tenant_id, import_id, row_number)
                WHERE superseded_at_utc IS NULL;
            CREATE INDEX ix_inventory_projection_import
                ON commercial.inventory_extraction_projections (
                    tenant_id, import_id, created_at_utc DESC);
            CREATE INDEX ix_inventory_candidates_current
                ON commercial.inventory_candidates (
                    tenant_id, import_id, status_code,
                    row_number, id)
                WHERE superseded_at_utc IS NULL;

            ALTER TABLE
                commercial.inventory_extraction_projections
                ENABLE ROW LEVEL SECURITY;
            ALTER TABLE
                commercial.inventory_extraction_projections
                FORCE ROW LEVEL SECURITY;
            CREATE POLICY inventory_projection_tenant_scope
                ON commercial.inventory_extraction_projections
                USING (
                    tenant_id =
                        commercial.current_tenant_id())
                WITH CHECK (
                    tenant_id =
                        commercial.current_tenant_id());
            CREATE TRIGGER protect_inventory_projections
                BEFORE UPDATE OR DELETE ON
                    commercial.inventory_extraction_projections
                FOR EACH ROW EXECUTE FUNCTION
                    commercial.reject_immutable_record_change();

            REVOKE ALL ON TABLE
                commercial.inventory_extraction_projections
                FROM PUBLIC;
            GRANT SELECT, INSERT ON TABLE
                commercial.inventory_extraction_projections
                TO advertified_app;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TRIGGER IF EXISTS protect_inventory_projections
                ON commercial.inventory_extraction_projections;
            DROP INDEX IF EXISTS
                commercial.ix_inventory_candidates_current;
            DROP INDEX IF EXISTS
                commercial.ux_inventory_candidates_current_import_row;
            ALTER TABLE commercial.inventory_candidates
                DROP CONSTRAINT IF EXISTS
                    fk_inventory_candidate_projection,
                DROP COLUMN IF EXISTS projection_id,
                DROP COLUMN IF EXISTS superseded_at_utc,
                ADD CONSTRAINT
                    ux_inventory_candidates_import_row
                    UNIQUE (tenant_id, import_id, row_number);
            ALTER TABLE
                commercial.inventory_extraction_attempts
                DROP CONSTRAINT IF EXISTS
                    fk_inventory_extraction_attempt_input,
                DROP COLUMN IF EXISTS input_artifact_id;
            DROP TABLE IF EXISTS
                commercial.inventory_extraction_projections;
            """);
    }
}
