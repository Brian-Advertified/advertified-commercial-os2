$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo
. (Join-Path $PSScriptRoot 'advertified-compose.ps1')
Assert-AdvertifiedComposeProject -RequireExisting

$outputRoot = Join-Path $repo 'artifacts/inventory-corpus/storage-audit'
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

$minio = @(& docker ps -q `
    --filter 'label=com.docker.compose.project=advertified-os2-dev' `
    --filter 'label=com.docker.compose.service=minio')
if ($LASTEXITCODE -ne 0 -or $minio.Count -ne 1) {
    throw 'Expected exactly one advertified-os2-dev MinIO container.'
}

$postgres = @(& docker ps -q `
    --filter 'label=com.docker.compose.project=advertified-os2-dev' `
    --filter 'label=com.docker.compose.service=postgres')
if ($LASTEXITCODE -ne 0 -or $postgres.Count -ne 1) {
    throw 'Expected exactly one advertified-os2-dev PostgreSQL container.'
}

$objects = & docker exec $minio[0] sh -c "ls -laR /data"
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to inspect the OS2 MinIO object store.'
}
$objects | Set-Content -LiteralPath `
    (Join-Path $outputRoot 'minio-files.txt') -Encoding utf8

$schema = & docker exec $postgres[0] psql `
    -U advertified -d advertified -v ON_ERROR_STOP=1 `
    -c '\d+ commercial.inventory_imports'
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to inspect the inventory import schema.'
}
$schema | Set-Content -LiteralPath `
    (Join-Path $outputRoot 'inventory-import-schema.txt') -Encoding utf8

Write-Host "Storage audit written to $outputRoot"
Write-Host "MinIO listing lines: $($objects.Count)"
Write-Host "Inventory schema lines: $($schema.Count)"
