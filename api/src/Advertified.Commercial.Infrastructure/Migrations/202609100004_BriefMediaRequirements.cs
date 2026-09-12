using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609100004_BriefMediaRequirements")]
public sealed class BriefMediaRequirements : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE commercial.brief_versions
            ADD COLUMN media_requirements_json jsonb NOT NULL DEFAULT '[]'::jsonb;

        ALTER TABLE commercial.brief_versions
            ADD CONSTRAINT ck_brief_versions_media_requirements_json
            CHECK (jsonb_typeof(media_requirements_json) = 'array');

        CREATE OR REPLACE FUNCTION commercial.reject_submitted_brief_content_change()
        RETURNS trigger
        LANGUAGE plpgsql
        AS $$
        BEGIN
            IF TG_OP = 'DELETE' THEN
                IF OLD.status_code <> 'DRAFT' THEN
                    RAISE EXCEPTION 'Submitted BriefVersions are immutable';
                END IF;
                RETURN OLD;
            END IF;
            IF OLD.status_code <> 'DRAFT' AND ROW(
                NEW.brief_id, NEW.base_version_id, NEW.source_id, NEW.version_no,
                NEW.business_problem, NEW.objective, NEW.audiences_json,
                NEW.geographies_json, NEW.timing, NEW.budget_minor,
                NEW.budget_unknown, NEW.currency_code, NEW.vat_status_code,
                NEW.fees_minor, NEW.media_requirements_json, NEW.constraints_json,
                NEW.measurement_json, NEW.facts_json, NEW.unknowns_json,
                NEW.assumptions_json, NEW.conflicts_json, NEW.evidence_bindings_json,
                NEW.created_by
            ) IS DISTINCT FROM ROW(
                OLD.brief_id, OLD.base_version_id, OLD.source_id, OLD.version_no,
                OLD.business_problem, OLD.objective, OLD.audiences_json,
                OLD.geographies_json, OLD.timing, OLD.budget_minor,
                OLD.budget_unknown, OLD.currency_code, OLD.vat_status_code,
                OLD.fees_minor, OLD.media_requirements_json, OLD.constraints_json,
                OLD.measurement_json, OLD.facts_json, OLD.unknowns_json,
                OLD.assumptions_json, OLD.conflicts_json, OLD.evidence_bindings_json,
                OLD.created_by
            ) THEN
                RAISE EXCEPTION 'Submitted BriefVersion content is immutable';
            END IF;
            RETURN NEW;
        END
        $$;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Brief media requirements are a forward-only canonical-data change.");
}
