using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609120014_MediaStrategyArtifactLineage")]
public sealed class MediaStrategyArtifactLineage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE commercial.media_mix_versions
            ADD COLUMN IF NOT EXISTS media_strategy_artifact_id uuid NULL;

        DO $$
        BEGIN
            IF NOT EXISTS (
                SELECT 1
                FROM pg_constraint
                WHERE conrelid = 'commercial.media_mix_versions'::regclass
                  AND conname = 'fk_media_mix_media_strategy_artifact') THEN
                ALTER TABLE commercial.media_mix_versions
                    ADD CONSTRAINT fk_media_mix_media_strategy_artifact
                    FOREIGN KEY (tenant_id, media_strategy_artifact_id)
                    REFERENCES commercial.intelligence_artifacts(tenant_id, id);
            END IF;
        END
        $$;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Media Strategy lineage repair is forward-only.");
}
