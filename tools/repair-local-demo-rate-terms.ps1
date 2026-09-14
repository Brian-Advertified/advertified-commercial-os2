param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'advertified-compose.ps1')
$container = 'advertified-os2-dev-postgres-1'
$projectionPath = Join-Path $repoRoot 'infrastructure\development\publish-current-inventory-to-marketplace.sql'

Push-Location $repoRoot
try {
    Assert-AdvertifiedComposeProject -RequireExisting
    $labelsJson = & docker inspect --format '{{json .Config.Labels}}' $container
    if ($LASTEXITCODE -ne 0) { throw 'Local Advertified PostgreSQL container could not be inspected.' }
    $labels = $labelsJson | ConvertFrom-Json
    if ($labels.'com.docker.compose.project' -ne 'advertified-os2-dev' -or
        $labels.'com.docker.compose.service' -ne 'postgres') {
        throw 'Refusing local fixture repair outside Advertified development PostgreSQL.'
    }

    $sql = @'
\set ON_ERROR_STOP on
BEGIN;
ALTER TABLE commercial.inventory_rates DISABLE TRIGGER USER;
UPDATE commercial.inventory_rates
SET commercial_terms_json = '{"rateValidFrom":"2026-01-01","rateValidTo":"2027-12-31","productionCostMinor":0,"installationCostMinor":0,"minimumOrder":1,"inclusions":[],"exclusions":[],"conditions":[],"billingDays":30}'::jsonb
WHERE tenant_id = '10000000-0000-0000-0000-000000000002'::uuid
  AND id IN (
    '10000000-0000-0000-0000-000000000130'::uuid,
    '10000000-0000-0000-0000-000000000131'::uuid
  )
  AND source_locator LIKE 'local-demo-proposal-inventory.csv#row=%';
ALTER TABLE commercial.inventory_rates ENABLE TRIGGER USER;

-- Correct the local digital demo product through a new immutable version. Historical
-- version 1 remains OOH if it was created before the channel classification was fixed.
INSERT INTO commercial.inventory_product_versions (
    id, tenant_id, product_id, version_number, name, channel_code,
    product_type_code, geography, latitude, longitude, verification_code,
    source_import_id, source_candidate_id, published_by, published_at_utc)
SELECT
    '10000000-0000-0000-0000-000000000122'::uuid,
    source.tenant_id, source.product_id, 2, source.name, 'DOOH',
    source.product_type_code, source.geography, source.latitude, source.longitude,
    source.verification_code, source.source_import_id, source.source_candidate_id,
    source.published_by, clock_timestamp()
FROM commercial.inventory_product_versions source
WHERE source.tenant_id = '10000000-0000-0000-0000-000000000002'::uuid
  AND source.id = '10000000-0000-0000-0000-000000000120'::uuid
  AND source.channel_code = 'OOH'
  AND NOT EXISTS (
      SELECT 1 FROM commercial.inventory_product_versions existing
      WHERE existing.id = '10000000-0000-0000-0000-000000000122'::uuid);

INSERT INTO commercial.inventory_rates (
    id, tenant_id, product_version_id, rate_type_code, currency_code,
    amount_minor, effective_from, effective_to, source_locator, commercial_terms_json)
SELECT
    '10000000-0000-0000-0000-000000000132'::uuid,
    source.tenant_id,
    '10000000-0000-0000-0000-000000000122'::uuid,
    source.rate_type_code, source.currency_code, source.amount_minor,
    source.effective_from, source.effective_to, source.source_locator,
    source.commercial_terms_json
FROM commercial.inventory_rates source
WHERE source.tenant_id = '10000000-0000-0000-0000-000000000002'::uuid
  AND source.id = '10000000-0000-0000-0000-000000000130'::uuid
  AND EXISTS (
      SELECT 1 FROM commercial.inventory_product_versions version
      WHERE version.id = '10000000-0000-0000-0000-000000000122'::uuid)
  AND NOT EXISTS (
      SELECT 1 FROM commercial.inventory_rates existing
      WHERE existing.id = '10000000-0000-0000-0000-000000000132'::uuid);

INSERT INTO commercial.inventory_availability (
    id, tenant_id, product_version_id, availability_code,
    observed_at_utc, valid_until_utc, source_locator)
SELECT
    '10000000-0000-0000-0000-000000000142'::uuid,
    source.tenant_id,
    '10000000-0000-0000-0000-000000000122'::uuid,
    source.availability_code, clock_timestamp(), source.valid_until_utc,
    source.source_locator
FROM commercial.inventory_availability source
WHERE source.tenant_id = '10000000-0000-0000-0000-000000000002'::uuid
  AND source.id = '10000000-0000-0000-0000-000000000140'::uuid
  AND EXISTS (
      SELECT 1 FROM commercial.inventory_product_versions version
      WHERE version.id = '10000000-0000-0000-0000-000000000122'::uuid)
  AND NOT EXISTS (
      SELECT 1 FROM commercial.inventory_availability existing
      WHERE existing.id = '10000000-0000-0000-0000-000000000142'::uuid);

UPDATE commercial.inventory_products product
SET current_version_id = '10000000-0000-0000-0000-000000000122'::uuid,
    version = product.version + 1,
    updated_at_utc = clock_timestamp()
WHERE product.tenant_id = '10000000-0000-0000-0000-000000000002'::uuid
  AND product.id = '10000000-0000-0000-0000-000000000110'::uuid
  AND product.current_version_id IS DISTINCT FROM '10000000-0000-0000-0000-000000000122'::uuid
  AND EXISTS (
      SELECT 1 FROM commercial.inventory_product_versions version
      WHERE version.id = '10000000-0000-0000-0000-000000000122'::uuid);
COMMIT;

SELECT 'local_demo_terms=' || count(*)
FROM commercial.inventory_rates
WHERE tenant_id = '10000000-0000-0000-0000-000000000002'::uuid
  AND id IN (
    '10000000-0000-0000-0000-000000000130'::uuid,
    '10000000-0000-0000-0000-000000000131'::uuid
  )
  AND (commercial_terms_json->>'billingDays')::int = 30;

SELECT 'local_demo_digital_channel=' || version.channel_code
FROM commercial.inventory_products product
JOIN commercial.inventory_product_versions version
  ON version.tenant_id = product.tenant_id
 AND version.id = product.current_version_id
WHERE product.tenant_id = '10000000-0000-0000-0000-000000000002'::uuid
  AND product.id = '10000000-0000-0000-0000-000000000110'::uuid;
'@
    $sql | & docker exec -i $container psql -U advertified -d advertified -v ON_ERROR_STOP=1 -q -A -t
    if ($LASTEXITCODE -ne 0) { throw 'Local demo inventory repair failed.' }

    if (-not (Test-Path $projectionPath)) { throw 'Marketplace projection SQL is missing.' }
    $projection = (Get-Content -Raw $projectionPath).Replace('\set ON_ERROR_STOP on', '')
    $projection | & docker exec -i $container psql -U advertified -d advertified -v ON_ERROR_STOP=1 -q -A -t
    if ($LASTEXITCODE -ne 0) { throw 'Marketplace snapshot refresh failed.' }
}
finally {
    Pop-Location
}
