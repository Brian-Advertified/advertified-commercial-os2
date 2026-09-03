using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609030045_InventoryExtractionWorkerRls")]
public sealed class InventoryExtractionWorkerRls : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        ReplacePolicy(migrationBuilder, OwnerAwarePolicySql);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        ReplacePolicy(migrationBuilder, TenantOnlyPolicySql);

    private static void ReplacePolicy(MigrationBuilder migrationBuilder, string sql) =>
        migrationBuilder.Sql(sql);

    private const string OwnerAwarePolicySql = """
        DROP POLICY inventory_extraction_attempts_tenant_scope
            ON commercial.inventory_extraction_attempts;
        CREATE POLICY inventory_extraction_attempts_tenant_scope
            ON commercial.inventory_extraction_attempts
            USING (
                tenant_id = commercial.current_tenant_id()
                OR current_user = 'advertified_migrator')
            WITH CHECK (
                tenant_id = commercial.current_tenant_id()
                OR current_user = 'advertified_migrator');
        """;

    private const string TenantOnlyPolicySql = """
        DROP POLICY inventory_extraction_attempts_tenant_scope
            ON commercial.inventory_extraction_attempts;
        CREATE POLICY inventory_extraction_attempts_tenant_scope
            ON commercial.inventory_extraction_attempts
            USING (tenant_id = commercial.current_tenant_id())
            WITH CHECK (tenant_id = commercial.current_tenant_id());
        """;
}
