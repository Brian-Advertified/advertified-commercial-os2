$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$sqlPath = Join-Path $repoRoot 'infrastructure\sql\202609040051_inventory_pending_supplier_rates.sql'
$reportPath = Join-Path $repoRoot 'artifacts\inventory-corpus\publication\pending-rate-migration.json'
$container = 'advertified-os2-dev-postgres-1'

$project = docker inspect $container --format '{{index .Config.Labels "com.docker.compose.project"}}'
if ($LASTEXITCODE -ne 0 -or $project.Trim() -ne 'advertified-os2-dev') {
    throw 'The OS2 PostgreSQL container was not found.'
}

$sql = Get-Content $sqlPath -Raw
$sql | docker exec -i $container sh -lc `
    'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
if ($LASTEXITCODE -ne 0) {
    throw 'Pending-supplier rate migration failed.'
}

$schema = docker exec $container sh -lc @'
psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -At -c "
SELECT COALESCE(jsonb_agg(to_jsonb(c)), '[]'::jsonb)
FROM (
  SELECT table_name, column_name, is_nullable
  FROM information_schema.columns
  WHERE table_schema = 'commercial'
    AND table_name LIKE '%inventory%rate%'
    AND column_name IN ('rate_type_code','currency_code','amount_minor','rate_amount_minor')
  ORDER BY table_name, column_name
) c;"
'@
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to verify pending-supplier rate schema.'
}
$columns = $schema | ConvertFrom-Json
$invalid = @($columns | Where-Object { $_.is_nullable -ne 'YES' })
if ($invalid.Count -gt 0) {
    throw 'One or more inventory rate columns still reject pending supplier values.'
}

$report = [ordered]@{
    schemaVersion = 'advertified.inventory-pending-rate-migration.v1'
    migration = '202609040051_inventory_pending_supplier_rates'
    sqlSha256 = (Get-FileHash $sqlPath -Algorithm SHA256).Hash.ToLowerInvariant()
    appliedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    composeProject = 'advertified-os2-dev'
    columns = @($columns)
    passed = $true
}
$directory = Split-Path $reportPath -Parent
New-Item -ItemType Directory -Force -Path $directory | Out-Null
$report | ConvertTo-Json -Depth 20 | Set-Content -Path $reportPath -Encoding utf8
Write-Host ($report | ConvertTo-Json -Depth 20)
