using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609020041_InventoryEmbeddingBackfill")]
public sealed class InventoryEmbeddingBackfill : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        """
        ALTER TABLE commercial.inventory_product_embeddings
            DROP CONSTRAINT IF EXISTS ux_inventory_embedding_input;
        CREATE INDEX IF NOT EXISTS ix_inventory_embedding_input
            ON commercial.inventory_product_embeddings (
                tenant_id, product_version_id, provider_code, model_code, input_hash);
        ALTER TABLE commercial.inventory_embedding_jobs
            ADD COLUMN is_explicit_backfill boolean NOT NULL DEFAULT false;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        """
        ALTER TABLE commercial.inventory_embedding_jobs
            DROP COLUMN is_explicit_backfill;
        DROP INDEX IF EXISTS commercial.ix_inventory_embedding_input;
        ALTER TABLE commercial.inventory_product_embeddings
            ADD CONSTRAINT ux_inventory_embedding_input UNIQUE (
                tenant_id, product_version_id, provider_code, model_code, input_hash);
        """);
}
