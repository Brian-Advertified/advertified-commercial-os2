using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202608300009_CampaignModeSelection")]
public sealed class CampaignModeSelection : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.audience_definition_sets
                ADD COLUMN target_audience_ids_json jsonb NOT NULL DEFAULT '[]'::jsonb,
                ADD COLUMN targeting_rationale text NOT NULL DEFAULT '',
                ADD COLUMN positioning_statement text NOT NULL DEFAULT '';

            CREATE TABLE commercial.campaign_mode_selections (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                brief_version_id uuid NOT NULL,
                mode_collection_code varchar(100) NOT NULL DEFAULT 'campaignModes',
                mode_code varchar(100) NOT NULL,
                decision_source_collection_code varchar(100) NOT NULL
                    DEFAULT 'campaignModeDecisionSources',
                decision_source_code varchar(100) NOT NULL,
                confidence numeric(5,4) NOT NULL,
                reason text,
                selected_by uuid NOT NULL,
                version bigint NOT NULL DEFAULT 1,
                selected_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_campaign_mode_tenant_id UNIQUE (tenant_id, id),
                CONSTRAINT ux_campaign_mode_brief UNIQUE (tenant_id, brief_version_id),
                CONSTRAINT ck_campaign_mode_collection CHECK (
                    mode_collection_code = 'campaignModes'),
                CONSTRAINT ck_campaign_mode_decision_source_collection CHECK (
                    decision_source_collection_code = 'campaignModeDecisionSources'),
                CONSTRAINT ck_campaign_mode_confidence CHECK (confidence BETWEEN 0 AND 1),
                CONSTRAINT ck_campaign_mode_version CHECK (version = 1),
                CONSTRAINT fk_campaign_mode_brief FOREIGN KEY (tenant_id, brief_version_id)
                    REFERENCES commercial.brief_versions (tenant_id, id),
                CONSTRAINT fk_campaign_mode_code FOREIGN KEY (
                    mode_collection_code, mode_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_campaign_mode_decision_source FOREIGN KEY (
                    decision_source_collection_code, decision_source_code)
                    REFERENCES governance.master_data_items (collection_code, code),
                CONSTRAINT fk_campaign_mode_selector FOREIGN KEY (selected_by)
                    REFERENCES commercial.users (id)
            );

            ALTER TABLE commercial.campaign_mode_selections ENABLE ROW LEVEL SECURITY;
            ALTER TABLE commercial.campaign_mode_selections FORCE ROW LEVEL SECURITY;
            CREATE POLICY campaign_mode_selections_tenant_scope
                ON commercial.campaign_mode_selections
                USING (tenant_id = commercial.current_tenant_id())
                WITH CHECK (tenant_id = commercial.current_tenant_id());

            CREATE TRIGGER protect_campaign_mode_selections
                BEFORE UPDATE OR DELETE ON commercial.campaign_mode_selections
                FOR EACH ROW EXECUTE FUNCTION commercial.reject_immutable_record_change();

            GRANT SELECT, INSERT ON commercial.campaign_mode_selections TO advertified_app;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TABLE IF EXISTS commercial.campaign_mode_selections;
            ALTER TABLE commercial.audience_definition_sets
                DROP COLUMN IF EXISTS positioning_statement,
                DROP COLUMN IF EXISTS targeting_rationale,
                DROP COLUMN IF EXISTS target_audience_ids_json;
            """);
    }
}
