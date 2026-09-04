$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo
. (Join-Path $PSScriptRoot 'advertified-compose.ps1')

$composeFiles = @(
    'infrastructure/docker-compose.yml',
    'infrastructure/docker-compose.app.yml',
    'artifacts/inventory-corpus/docker-compose.override.yml'
)

Assert-AdvertifiedComposeProject -RequireExisting

$foreignContainers = @(
    docker ps -a `
        --filter 'label=com.docker.compose.project=advertified-dev' `
        --format '{{.ID}}'
)
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to inspect accidental advertified-dev containers.'
}
if ($foreignContainers.Count -gt 0) {
    throw 'Refusing API reload while advertified-dev containers still exist.'
}

$accidentalVolumes = @(
    'advertified-dev_minio_data',
    'advertified-dev_clamav_data',
    'advertified-dev_postgres_data',
    'advertified-dev_redis_data'
)
foreach ($volume in $accidentalVolumes) {
    docker volume inspect $volume *> $null
    if ($LASTEXITCODE -ne 0) {
        continue
    }
    $inspectionJson = docker volume inspect $volume
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to inspect accidental volume '$volume'."
    }
    $inspection = @($inspectionJson | ConvertFrom-Json)
    $project = $inspection[0].Labels.'com.docker.compose.project'
    if ($project -ne 'advertified-dev') {
        throw "Refusing to remove volume '$volume': project label is not advertified-dev."
    }
    docker volume rm $volume | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to remove accidental unused volume '$volume'."
    }
}

Write-Host 'Reloading only the existing advertified-os2-dev API from the current image...'
Invoke-AdvertifiedCompose $composeFiles @(
    'up', '-d', '--no-build', '--no-deps', '--force-recreate', 'api'
)
Wait-AdvertifiedService $composeFiles 'api'
Assert-AdvertifiedComposeProject -RequireExisting
Invoke-AdvertifiedCompose $composeFiles @('ps', 'api')
