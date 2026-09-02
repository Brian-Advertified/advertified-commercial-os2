using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609020037_InventoryAudienceMatching")]
public sealed class InventoryAudienceMatching : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.inventory_product_versions
                ADD COLUMN audience_profile_json jsonb,
                ADD CONSTRAINT ck_inventory_product_audience_profile CHECK (
                    audience_profile_json IS NULL OR
                    (jsonb_typeof(audience_profile_json) = 'object' AND
                     audience_profile_json ?& ARRAY['spokenLanguages',
                         'understoodLanguages', 'lifeStages', 'lsmSemSegments'] AND
                     jsonb_typeof(audience_profile_json->'spokenLanguages') = 'array' AND
                     jsonb_typeof(audience_profile_json->'understoodLanguages') = 'array' AND
                     jsonb_typeof(audience_profile_json->'lifeStages') = 'array' AND
                     jsonb_typeof(audience_profile_json->'lsmSemSegments') = 'array'));

            ALTER TABLE commercial.audience_definitions
                ADD COLUMN lsm_sem_taxonomy varchar(200),
                ADD COLUMN lsm_sem_taxonomy_version varchar(100),
                ADD CONSTRAINT ck_audience_lsm_taxonomy_pair CHECK (
                    (lsm_sem_taxonomy IS NULL AND lsm_sem_taxonomy_version IS NULL)
                    OR (btrim(lsm_sem_taxonomy) <> '' AND
                        btrim(lsm_sem_taxonomy_version) <> ''));

            ALTER TABLE commercial.marketplace_listing_versions
                ADD COLUMN audience_profile_json jsonb,
                ADD CONSTRAINT ck_marketplace_listing_audience_profile CHECK (
                    audience_profile_json IS NULL OR
                    (jsonb_typeof(audience_profile_json) = 'object' AND
                     audience_profile_json ?& ARRAY['spokenLanguages',
                         'understoodLanguages', 'lifeStages', 'lsmSemSegments'] AND
                     jsonb_typeof(audience_profile_json->'spokenLanguages') = 'array' AND
                     jsonb_typeof(audience_profile_json->'understoodLanguages') = 'array' AND
                     jsonb_typeof(audience_profile_json->'lifeStages') = 'array' AND
                     jsonb_typeof(audience_profile_json->'lsmSemSegments') = 'array'));

            ALTER TABLE commercial.inventory_shortlist_candidates
                ADD COLUMN audience_fit_json jsonb NOT NULL DEFAULT
                    '{"languageScore":null,"lifeStageScore":null,"lsmSemScore":null,"evidenceGaps":[],"measurementSource":null,"measurementPeriod":null,"methodology":null,"taxonomyName":null,"taxonomyVersion":null}'::jsonb,
                ADD CONSTRAINT ck_shortlist_audience_fit CHECK (
                    jsonb_typeof(audience_fit_json) = 'object'
                    AND audience_fit_json ?& ARRAY[
                        'languageScore', 'lifeStageScore', 'lsmSemScore', 'evidenceGaps',
                        'measurementSource', 'measurementPeriod', 'methodology',
                        'taxonomyName', 'taxonomyVersion']
                    AND jsonb_typeof(audience_fit_json->'evidenceGaps') = 'array'
                    AND (audience_fit_json->'languageScore' = 'null'::jsonb OR
                        (jsonb_typeof(audience_fit_json->'languageScore') = 'number' AND
                         (audience_fit_json->>'languageScore')::numeric BETWEEN 0 AND 1))
                    AND (audience_fit_json->'lifeStageScore' = 'null'::jsonb OR
                        (jsonb_typeof(audience_fit_json->'lifeStageScore') = 'number' AND
                         (audience_fit_json->>'lifeStageScore')::numeric BETWEEN 0 AND 1))
                    AND (audience_fit_json->'lsmSemScore' = 'null'::jsonb OR
                        (jsonb_typeof(audience_fit_json->'lsmSemScore') = 'number' AND
                         (audience_fit_json->>'lsmSemScore')::numeric BETWEEN 0 AND 1)));
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE commercial.inventory_shortlist_candidates
                DROP CONSTRAINT IF EXISTS ck_shortlist_audience_fit,
                DROP COLUMN IF EXISTS audience_fit_json;
            ALTER TABLE commercial.marketplace_listing_versions
                DROP CONSTRAINT IF EXISTS ck_marketplace_listing_audience_profile,
                DROP COLUMN IF EXISTS audience_profile_json;
            ALTER TABLE commercial.inventory_product_versions
                DROP CONSTRAINT IF EXISTS ck_inventory_product_audience_profile,
                DROP COLUMN IF EXISTS audience_profile_json;
            ALTER TABLE commercial.audience_definitions
                DROP CONSTRAINT IF EXISTS ck_audience_lsm_taxonomy_pair,
                DROP COLUMN IF EXISTS lsm_sem_taxonomy_version,
                DROP COLUMN IF EXISTS lsm_sem_taxonomy;
            """);
    }
}
