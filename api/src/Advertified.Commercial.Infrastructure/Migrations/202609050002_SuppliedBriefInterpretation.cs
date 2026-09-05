using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609050002_SuppliedBriefInterpretation")]
public sealed class SuppliedBriefInterpretation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE commercial.supplied_brief_interpretations (
            id uuid PRIMARY KEY,
            tenant_id uuid NOT NULL REFERENCES commercial.tenants(id),
            actor_id uuid NOT NULL REFERENCES commercial.users(id),
            parent_id uuid,
            version_no integer NOT NULL CHECK (version_no > 0),
            source_title text NOT NULL,
            source_content text NOT NULL,
            source_hash varchar(64) NOT NULL CHECK (
                source_hash = encode(public.digest(convert_to(source_content, 'UTF8'), 'sha256'), 'hex')),
            input_hash varchar(64) NOT NULL CHECK (input_hash ~ '^[0-9a-f]{64}$'),
            clarifications_json jsonb NOT NULL,
            created_at_utc timestamptz NOT NULL,
            UNIQUE (tenant_id, id),
            UNIQUE (tenant_id, actor_id, id),
            FOREIGN KEY (tenant_id, actor_id, parent_id)
                REFERENCES commercial.supplied_brief_interpretations(tenant_id, actor_id, id)
        );
        ALTER TABLE commercial.supplied_brief_interpretations ENABLE ROW LEVEL SECURITY;
        ALTER TABLE commercial.supplied_brief_interpretations FORCE ROW LEVEL SECURITY;
        CREATE POLICY supplied_brief_interpretation_scope
            ON commercial.supplied_brief_interpretations
            USING (tenant_id = commercial.current_tenant_id() AND actor_id = commercial.current_user_id())
            WITH CHECK (tenant_id = commercial.current_tenant_id() AND actor_id = commercial.current_user_id());
        GRANT SELECT, INSERT ON commercial.supplied_brief_interpretations TO advertified_app;
        CREATE TRIGGER supplied_brief_interpretation_immutable
            BEFORE UPDATE OR DELETE ON commercial.supplied_brief_interpretations
            FOR EACH ROW EXECUTE FUNCTION commercial.reject_immutable_record_change();
        ALTER TABLE commercial.agent_runs ADD COLUMN supplied_brief_interpretation_id uuid;
        ALTER TABLE commercial.agent_runs ADD CONSTRAINT fk_agent_runs_supplied_brief
            FOREIGN KEY (tenant_id, supplied_brief_interpretation_id)
            REFERENCES commercial.supplied_brief_interpretations(tenant_id, id);
        ALTER TABLE commercial.agent_runs DROP CONSTRAINT ck_agent_runs_work_scope;
        ALTER TABLE commercial.agent_runs ADD CONSTRAINT ck_agent_runs_work_scope CHECK (
            opportunity_id IS NOT NULL OR campaign_id IS NOT NULL OR supplied_brief_interpretation_id IS NOT NULL);
        ALTER TABLE commercial.brief_sources ADD COLUMN interpretation_id uuid;
        ALTER TABLE commercial.brief_sources ADD CONSTRAINT fk_brief_sources_interpretation
            FOREIGN KEY (tenant_id, interpretation_id)
            REFERENCES commercial.supplied_brief_interpretations(tenant_id, id);
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DO $$ BEGIN
            IF EXISTS (SELECT 1 FROM commercial.supplied_brief_interpretations) THEN
                RAISE EXCEPTION 'Retained supplied Brief evidence prevents destructive downgrade';
            END IF;
        END $$;
        ALTER TABLE commercial.brief_sources DROP COLUMN interpretation_id;
        ALTER TABLE commercial.agent_runs DROP CONSTRAINT ck_agent_runs_work_scope;
        ALTER TABLE commercial.agent_runs ADD CONSTRAINT ck_agent_runs_work_scope CHECK (
            opportunity_id IS NOT NULL OR campaign_id IS NOT NULL);
        ALTER TABLE commercial.agent_runs DROP COLUMN supplied_brief_interpretation_id;
        DROP TABLE commercial.supplied_brief_interpretations;
        """);
}
