using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

public sealed partial class InventoryTruth
{
    private static readonly string[] InventoryTenantTables =
    [
        "inventory_suppliers",
        "inventory_imports",
        "inventory_import_steps",
        "inventory_candidates",
        "inventory_candidate_fields",
        "inventory_review_decisions",
        "inventory_products",
        "inventory_product_versions",
        "inventory_rates",
        "inventory_availability",
        "inventory_assets",
    ];

    private static void CreateInventorySecurityBoundary(MigrationBuilder migrationBuilder)
    {
        foreach (var table in InventoryTenantTables)
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
            CREATE TRIGGER protect_inventory_candidate_fields
                BEFORE UPDATE OR DELETE ON commercial.inventory_candidate_fields
                FOR EACH ROW EXECUTE FUNCTION commercial.reject_immutable_record_change();
            CREATE TRIGGER protect_inventory_review_decisions
                BEFORE UPDATE OR DELETE ON commercial.inventory_review_decisions
                FOR EACH ROW EXECUTE FUNCTION commercial.reject_immutable_record_change();
            CREATE TRIGGER protect_inventory_product_versions
                BEFORE UPDATE OR DELETE ON commercial.inventory_product_versions
                FOR EACH ROW EXECUTE FUNCTION commercial.reject_immutable_record_change();
            CREATE TRIGGER protect_inventory_rates
                BEFORE UPDATE OR DELETE ON commercial.inventory_rates
                FOR EACH ROW EXECUTE FUNCTION commercial.reject_immutable_record_change();
            CREATE TRIGGER protect_inventory_availability
                BEFORE UPDATE OR DELETE ON commercial.inventory_availability
                FOR EACH ROW EXECUTE FUNCTION commercial.reject_immutable_record_change();
            CREATE TRIGGER protect_inventory_assets
                BEFORE UPDATE OR DELETE ON commercial.inventory_assets
                FOR EACH ROW EXECUTE FUNCTION commercial.reject_immutable_record_change();

            GRANT SELECT, INSERT, UPDATE ON
                commercial.inventory_suppliers,
                commercial.inventory_imports,
                commercial.inventory_import_steps,
                commercial.inventory_candidates,
                commercial.inventory_candidate_fields,
                commercial.inventory_review_decisions,
                commercial.inventory_products,
                commercial.inventory_product_versions,
                commercial.inventory_rates,
                commercial.inventory_availability,
                commercial.inventory_assets
                TO advertified_app;
            """);
    }
}
