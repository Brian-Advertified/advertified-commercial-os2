using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class CanonicalPlanning
{
    private static readonly string[] PlanningTenantTables =
    [
        "audience_definition_sets",
        "audience_definitions",
        "media_mix_versions",
        "inventory_shortlist_versions",
        "inventory_shortlist_candidates",
        "inventory_benchmark_snapshots",
        "shortlist_selections",
        "media_plan_versions",
        "media_plan_lines",
        "recommendation_bindings",
        "supply_coordination",
        "planning_objection_resolutions",
    ];

    private static readonly string[] ImmutablePlanningTables =
    [
        "audience_definition_sets",
        "audience_definitions",
        "inventory_shortlist_candidates",
        "inventory_benchmark_snapshots",
        "shortlist_selections",
        "media_plan_lines",
        "recommendation_bindings",
        "supply_coordination",
        "planning_objection_resolutions",
    ];

    private static void CreatePlanningSecurityBoundary(MigrationBuilder migrationBuilder)
    {
        foreach (var table in PlanningTenantTables)
        {
            migrationBuilder.Sql($"""
                ALTER TABLE commercial.{table} ENABLE ROW LEVEL SECURITY;
                ALTER TABLE commercial.{table} FORCE ROW LEVEL SECURITY;
                CREATE POLICY {table}_tenant_scope ON commercial.{table}
                    USING (tenant_id = commercial.current_tenant_id())
                    WITH CHECK (tenant_id = commercial.current_tenant_id());
                """);
        }
        foreach (var table in ImmutablePlanningTables)
        {
            migrationBuilder.Sql($"""
                CREATE TRIGGER protect_{table}
                    BEFORE UPDATE OR DELETE ON commercial.{table}
                    FOR EACH ROW EXECUTE FUNCTION commercial.reject_immutable_record_change();
                """);
        }
        migrationBuilder.Sql(
            """
            GRANT SELECT, INSERT, UPDATE ON
                commercial.audience_definition_sets,
                commercial.audience_definitions,
                commercial.media_mix_versions,
                commercial.inventory_shortlist_versions,
                commercial.inventory_shortlist_candidates,
                commercial.inventory_benchmark_snapshots,
                commercial.shortlist_selections,
                commercial.media_plan_versions,
                commercial.media_plan_lines,
                commercial.recommendation_bindings,
                commercial.supply_coordination,
                commercial.planning_objection_resolutions
                TO advertified_app;
            """);
    }
}
