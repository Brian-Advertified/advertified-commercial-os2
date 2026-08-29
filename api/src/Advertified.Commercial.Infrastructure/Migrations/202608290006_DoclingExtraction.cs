using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202608290006_DoclingExtraction")]
public sealed class DoclingExtraction : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE commercial.inventory_extractions (
                id uuid NOT NULL,
                tenant_id uuid NOT NULL,
                import_id uuid NOT NULL,
                source_hash char(64) NOT NULL,
                adapter_code varchar(100) NOT NULL,
                adapter_version varchar(200) NOT NULL,
                schema_version varchar(200) NOT NULL,
                structured_json jsonb NOT NULL,
                output_hash char(64) NOT NULL,
                completed_at_utc timestamptz NOT NULL,
                PRIMARY KEY (id),
                CONSTRAINT ux_inventory_extractions_import UNIQUE (tenant_id, import_id),
                CONSTRAINT ck_inventory_extractions_source_hash
                    CHECK (source_hash ~ '^[0-9a-f]{64}$'),
                CONSTRAINT ck_inventory_extractions_output_hash
                    CHECK (output_hash ~ '^[0-9a-f]{64}$'),
                CONSTRAINT fk_inventory_extractions_import FOREIGN KEY (tenant_id, import_id)
                    REFERENCES commercial.inventory_imports (tenant_id, id)
            );
            CREATE INDEX ix_inventory_extractions_checkpoint
                ON commercial.inventory_extractions
                (tenant_id, source_hash, adapter_code, adapter_version);

            ALTER TABLE commercial.inventory_extractions ENABLE ROW LEVEL SECURITY;
            ALTER TABLE commercial.inventory_extractions FORCE ROW LEVEL SECURITY;
            CREATE POLICY inventory_extractions_tenant_scope
                ON commercial.inventory_extractions
                USING (tenant_id = commercial.current_tenant_id())
                WITH CHECK (tenant_id = commercial.current_tenant_id());
            CREATE TRIGGER protect_inventory_extractions
                BEFORE UPDATE OR DELETE ON commercial.inventory_extractions
                FOR EACH ROW EXECUTE FUNCTION commercial.reject_immutable_record_change();
            GRANT SELECT, INSERT ON commercial.inventory_extractions TO advertified_app;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS commercial.inventory_extractions;");
    }
}
