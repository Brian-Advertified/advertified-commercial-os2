using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class CanonicalBrief
{
    private static readonly string[] BriefTenantTables =
    [
        "campaign_briefs",
        "brief_sources",
        "brief_versions",
        "brief_version_evidence_items",
    ];

    private static void CreateBriefSecurityBoundary(MigrationBuilder migrationBuilder)
    {
        foreach (var table in BriefTenantTables)
        {
            migrationBuilder.Sql($"""
                ALTER TABLE commercial.{table} ENABLE ROW LEVEL SECURITY;
                ALTER TABLE commercial.{table} FORCE ROW LEVEL SECURITY;
                CREATE POLICY {table}_tenant_scope ON commercial.{table}
                    USING (tenant_id = commercial.current_tenant_id())
                    WITH CHECK (tenant_id = commercial.current_tenant_id());
                """);
        }

        migrationBuilder.Sql(
            """
            CREATE TRIGGER protect_brief_sources
                BEFORE UPDATE OR DELETE ON commercial.brief_sources
                FOR EACH ROW EXECUTE FUNCTION commercial.reject_immutable_record_change();

            CREATE FUNCTION commercial.reject_submitted_brief_content_change()
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
                    NEW.fees_minor, NEW.constraints_json, NEW.measurement_json,
                    NEW.facts_json, NEW.unknowns_json, NEW.assumptions_json,
                    NEW.conflicts_json, NEW.evidence_bindings_json, NEW.created_by
                ) IS DISTINCT FROM ROW(
                    OLD.brief_id, OLD.base_version_id, OLD.source_id, OLD.version_no,
                    OLD.business_problem, OLD.objective, OLD.audiences_json,
                    OLD.geographies_json, OLD.timing, OLD.budget_minor,
                    OLD.budget_unknown, OLD.currency_code, OLD.vat_status_code,
                    OLD.fees_minor, OLD.constraints_json, OLD.measurement_json,
                    OLD.facts_json, OLD.unknowns_json, OLD.assumptions_json,
                    OLD.conflicts_json, OLD.evidence_bindings_json, OLD.created_by
                ) THEN
                    RAISE EXCEPTION 'Submitted BriefVersion content is immutable';
                END IF;
                RETURN NEW;
            END
            $$;

            CREATE TRIGGER protect_submitted_brief_versions
                BEFORE UPDATE OR DELETE ON commercial.brief_versions
                FOR EACH ROW EXECUTE FUNCTION commercial.reject_submitted_brief_content_change();

            GRANT SELECT, INSERT, UPDATE ON
                commercial.campaign_briefs,
                commercial.brief_sources,
                commercial.brief_versions,
                commercial.brief_version_evidence_items
                TO advertified_app;
            """);
    }
}
