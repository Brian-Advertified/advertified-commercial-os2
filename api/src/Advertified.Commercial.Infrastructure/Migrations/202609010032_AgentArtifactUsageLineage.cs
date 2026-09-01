using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609010032_AgentArtifactUsageLineage")]
public sealed class AgentArtifactUsageLineage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.audience_definition_sets
                ADD COLUMN agent_provider_code varchar(100),
                ADD COLUMN agent_model_code varchar(300),
                ADD COLUMN agent_incremental_cost_minor bigint,
                ADD COLUMN agent_provider_request_id varchar(300),
                ADD CONSTRAINT ck_audience_agent_usage CHECK (
                    (agent_provider_code IS NULL AND agent_model_code IS NULL
                        AND agent_incremental_cost_minor IS NULL
                        AND agent_provider_request_id IS NULL)
                    OR commercial.valid_agent_usage_metadata(
                        agent_provider_code, agent_model_code,
                        agent_incremental_cost_minor, agent_provider_request_id));

            ALTER TABLE commercial.media_mix_versions
                ADD COLUMN agent_provider_code varchar(100),
                ADD COLUMN agent_model_code varchar(300),
                ADD COLUMN agent_incremental_cost_minor bigint,
                ADD COLUMN agent_provider_request_id varchar(300),
                ADD CONSTRAINT ck_media_mix_agent_usage CHECK (
                    (agent_provider_code IS NULL AND agent_model_code IS NULL
                        AND agent_incremental_cost_minor IS NULL
                        AND agent_provider_request_id IS NULL)
                    OR commercial.valid_agent_usage_metadata(
                        agent_provider_code, agent_model_code,
                        agent_incremental_cost_minor, agent_provider_request_id));

            ALTER TABLE commercial.inventory_shortlist_versions
                ADD COLUMN agent_provider_code varchar(100),
                ADD COLUMN agent_model_code varchar(300),
                ADD COLUMN agent_incremental_cost_minor bigint,
                ADD COLUMN agent_provider_request_id varchar(300),
                ADD CONSTRAINT ck_shortlist_agent_usage CHECK (
                    (agent_provider_code IS NULL AND agent_model_code IS NULL
                        AND agent_incremental_cost_minor IS NULL
                        AND agent_provider_request_id IS NULL)
                    OR commercial.valid_agent_usage_metadata(
                        agent_provider_code, agent_model_code,
                        agent_incremental_cost_minor, agent_provider_request_id));

            ALTER TABLE commercial.proposal_versions
                ADD COLUMN agent_provider_code varchar(100),
                ADD COLUMN agent_model_code varchar(300),
                ADD COLUMN agent_incremental_cost_minor bigint,
                ADD COLUMN agent_provider_request_id varchar(300),
                ADD CONSTRAINT ck_proposal_agent_usage CHECK (
                    (agent_provider_code IS NULL AND agent_model_code IS NULL
                        AND agent_incremental_cost_minor IS NULL
                        AND agent_provider_request_id IS NULL)
                    OR commercial.valid_agent_usage_metadata(
                        agent_provider_code, agent_model_code,
                        agent_incremental_cost_minor, agent_provider_request_id));
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.proposal_versions
                DROP CONSTRAINT ck_proposal_agent_usage,
                DROP COLUMN agent_provider_request_id,
                DROP COLUMN agent_incremental_cost_minor,
                DROP COLUMN agent_model_code,
                DROP COLUMN agent_provider_code;
            ALTER TABLE commercial.inventory_shortlist_versions
                DROP CONSTRAINT ck_shortlist_agent_usage,
                DROP COLUMN agent_provider_request_id,
                DROP COLUMN agent_incremental_cost_minor,
                DROP COLUMN agent_model_code,
                DROP COLUMN agent_provider_code;
            ALTER TABLE commercial.media_mix_versions
                DROP CONSTRAINT ck_media_mix_agent_usage,
                DROP COLUMN agent_provider_request_id,
                DROP COLUMN agent_incremental_cost_minor,
                DROP COLUMN agent_model_code,
                DROP COLUMN agent_provider_code;
            ALTER TABLE commercial.audience_definition_sets
                DROP CONSTRAINT ck_audience_agent_usage,
                DROP COLUMN agent_provider_request_id,
                DROP COLUMN agent_incremental_cost_minor,
                DROP COLUMN agent_model_code,
                DROP COLUMN agent_provider_code;
            """);
    }
}
