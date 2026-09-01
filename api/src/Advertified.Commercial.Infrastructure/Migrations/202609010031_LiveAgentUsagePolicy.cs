using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609010031_LiveAgentUsagePolicy")]
public sealed class LiveAgentUsagePolicy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE FUNCTION commercial.valid_agent_usage_metadata(
                provider_code varchar,
                model_code varchar,
                incremental_cost_minor bigint,
                provider_request_id varchar)
            RETURNS boolean
            LANGUAGE sql
            IMMUTABLE
            PARALLEL SAFE
            AS $valid_agent_usage_metadata$
                SELECT COALESCE(
                    incremental_cost_minor >= 0 AND (
                        (provider_code = 'deterministic'
                            AND model_code = 'fixture-v1'
                            AND incremental_cost_minor = 0
                            AND provider_request_id IS NULL)
                        OR
                        (provider_code = 'bedrock'
                            AND btrim(COALESCE(model_code, '')) <> ''
                            AND model_code <> 'fixture-v1'
                            AND btrim(COALESCE(provider_request_id, '')) <> '')
                    ),
                    FALSE);
            $valid_agent_usage_metadata$;
            REVOKE ALL ON FUNCTION commercial.valid_agent_usage_metadata(
                varchar, varchar, bigint, varchar) FROM PUBLIC;
            GRANT EXECUTE ON FUNCTION commercial.valid_agent_usage_metadata(
                varchar, varchar, bigint, varchar) TO advertified_app;

            ALTER TABLE commercial.ai_usage_ledger
                DROP CONSTRAINT ck_ai_usage_gate4_cost,
                ALTER COLUMN model_code TYPE varchar(300),
                ADD COLUMN provider_request_id varchar(300),
                ADD CONSTRAINT ck_ai_usage_provider_shape CHECK (
                    tool_calls = 0 AND (
                        (provider_code = 'deterministic'
                            AND model_code = 'fixture-v1'
                            AND units = 0
                            AND incremental_cost_minor = 0
                            AND cache_status_code = 'FIXTURE'
                            AND provider_request_id IS NULL)
                        OR
                        (provider_code = 'bedrock'
                            AND model_code <> 'fixture-v1'
                            AND units > 0
                            AND incremental_cost_minor >= 0
                            AND cache_status_code IN ('LIVE', 'CACHE_HIT')
                            AND btrim(COALESCE(provider_request_id, '')) <> '')
                    ));

            ALTER TABLE commercial.measurement_report_versions
                DROP CONSTRAINT ck_measurement_report_agent,
                ALTER COLUMN model_code TYPE varchar(300),
                ADD COLUMN provider_request_id varchar(300),
                ADD CONSTRAINT ck_measurement_report_agent CHECK (
                    agent_contract_version = '1.0.0'
                    AND prompt_version = '1.0.0'
                    AND tool_calls = 0
                    AND output_validated = true
                    AND (
                        (provider_code = 'deterministic'
                            AND model_code = 'fixture-v1'
                            AND incremental_cost_minor = 0
                            AND provider_request_id IS NULL)
                        OR
                        (provider_code = 'bedrock'
                            AND model_code <> 'fixture-v1'
                            AND incremental_cost_minor >= 0
                            AND btrim(COALESCE(provider_request_id, '')) <> '')
                    ));
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM commercial.ai_usage_ledger
                    WHERE provider_code <> 'deterministic'
                       OR incremental_cost_minor <> 0
                       OR provider_request_id IS NOT NULL
                ) OR EXISTS (
                    SELECT 1 FROM commercial.measurement_report_versions
                    WHERE provider_code <> 'deterministic'
                       OR incremental_cost_minor <> 0
                       OR provider_request_id IS NOT NULL
                ) THEN
                    RAISE EXCEPTION
                        'Cannot roll back live agent usage policy after live-provider evidence exists.';
                END IF;
            END
            $$;

            ALTER TABLE commercial.measurement_report_versions
                DROP CONSTRAINT ck_measurement_report_agent,
                DROP COLUMN provider_request_id,
                ALTER COLUMN model_code TYPE varchar(100),
                ADD CONSTRAINT ck_measurement_report_agent CHECK (
                    agent_contract_version = '1.0.0' AND prompt_version = '1.0.0'
                    AND provider_code = 'deterministic' AND model_code = 'fixture-v1'
                    AND tool_calls = 0 AND incremental_cost_minor = 0
                    AND output_validated = true);

            ALTER TABLE commercial.ai_usage_ledger
                DROP CONSTRAINT ck_ai_usage_provider_shape,
                DROP COLUMN provider_request_id,
                ALTER COLUMN model_code TYPE varchar(100),
                ADD CONSTRAINT ck_ai_usage_gate4_cost CHECK (incremental_cost_minor = 0);

            DROP FUNCTION commercial.valid_agent_usage_metadata(
                varchar, varchar, bigint, varchar);
            """);
    }
}
