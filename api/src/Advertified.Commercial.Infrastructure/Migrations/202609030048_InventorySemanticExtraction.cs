using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609030048_InventorySemanticExtraction")]
public sealed class InventorySemanticExtraction : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE commercial.inventory_semantic_runs (
                id uuid NOT NULL PRIMARY KEY,
                tenant_id uuid NOT NULL,
                import_id uuid NOT NULL,
                extraction_attempt_id uuid NOT NULL,
                source_hash char(64) NOT NULL,
                input_hash char(64) NOT NULL,
                budget_scope varchar(200) NOT NULL,
                prompt_version varchar(30) NOT NULL,
                model_code varchar(300) NOT NULL,
                chunk_number integer NOT NULL,
                chunk_count integer NOT NULL,
                status_collection_code varchar(100) NOT NULL
                    DEFAULT 'lifecycleStatuses',
                status_code varchar(100) NOT NULL,
                request_json jsonb NOT NULL,
                response_json jsonb,
                maximum_cost_usd_micros bigint NOT NULL,
                incremental_cost_usd_micros bigint,
                input_tokens integer,
                output_tokens integer,
                provider_request_id varchar(300),
                failure_code varchar(100),
                requested_by uuid NOT NULL,
                created_at_utc timestamptz NOT NULL,
                started_at_utc timestamptz,
                completed_at_utc timestamptz,
                version bigint NOT NULL DEFAULT 1,
                CONSTRAINT ux_inventory_semantic_runs_tenant_id
                    UNIQUE (tenant_id, id),
                CONSTRAINT ux_inventory_semantic_runs_cache
                    UNIQUE (
                        tenant_id, input_hash,
                        prompt_version, model_code),
                CONSTRAINT ck_inventory_semantic_hashes CHECK (
                    source_hash ~ '^[0-9a-f]{64}$'
                    AND input_hash ~ '^[0-9a-f]{64}$'),
                CONSTRAINT ck_inventory_semantic_chunks CHECK (
                    chunk_number > 0
                    AND chunk_count >= chunk_number
                    AND chunk_count <= 256),
                CONSTRAINT ck_inventory_semantic_cost CHECK (
                    maximum_cost_usd_micros > 0
                    AND maximum_cost_usd_micros <= 5000000
                    AND (
                        incremental_cost_usd_micros IS NULL
                        OR incremental_cost_usd_micros BETWEEN 0
                            AND maximum_cost_usd_micros)),
                CONSTRAINT ck_inventory_semantic_usage CHECK (
                    (status_code <> 'COMPLETED')
                    OR (
                        response_json IS NOT NULL
                        AND incremental_cost_usd_micros IS NOT NULL
                        AND input_tokens IS NOT NULL
                        AND input_tokens >= 0
                        AND output_tokens IS NOT NULL
                        AND output_tokens > 0
                        AND provider_request_id IS NOT NULL
                        AND btrim(provider_request_id) <> ''
                        AND completed_at_utc IS NOT NULL)),
                CONSTRAINT ck_inventory_semantic_status_collection
                    CHECK (
                        status_collection_code =
                            'lifecycleStatuses'),
                CONSTRAINT fk_inventory_semantic_import
                    FOREIGN KEY (tenant_id, import_id)
                    REFERENCES commercial.inventory_imports (
                        tenant_id, id),
                CONSTRAINT fk_inventory_semantic_attempt
                    FOREIGN KEY (
                        tenant_id, extraction_attempt_id)
                    REFERENCES
                        commercial.inventory_extraction_attempts (
                            tenant_id, id),
                CONSTRAINT fk_inventory_semantic_status
                    FOREIGN KEY (
                        status_collection_code, status_code)
                    REFERENCES governance.master_data_items (
                        collection_code, code),
                CONSTRAINT fk_inventory_semantic_requester
                    FOREIGN KEY (requested_by)
                    REFERENCES commercial.users (id)
            );

            CREATE INDEX ix_inventory_semantic_budget
                ON commercial.inventory_semantic_runs (
                    tenant_id, budget_scope, status_code,
                    created_at_utc);
            CREATE INDEX ix_inventory_semantic_import
                ON commercial.inventory_semantic_runs (
                    tenant_id, import_id, chunk_number);

            ALTER TABLE commercial.inventory_semantic_runs
                ENABLE ROW LEVEL SECURITY;
            ALTER TABLE commercial.inventory_semantic_runs
                FORCE ROW LEVEL SECURITY;
            CREATE POLICY inventory_semantic_runs_tenant_scope
                ON commercial.inventory_semantic_runs
                USING (
                    tenant_id =
                        commercial.current_tenant_id())
                WITH CHECK (
                    tenant_id =
                        commercial.current_tenant_id());
            REVOKE ALL ON TABLE
                commercial.inventory_semantic_runs
                FROM PUBLIC;
            GRANT SELECT, INSERT, UPDATE ON TABLE
                commercial.inventory_semantic_runs
                TO advertified_app;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TABLE commercial.inventory_semantic_runs;
            """);
    }
}
