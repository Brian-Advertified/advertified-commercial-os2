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
Write-Host 'Building only the existing OS2 API image...'
& docker build `
    --file 'api/Dockerfile' `
    --target 'api' `
    --tag 'advertified/commercial-api-dev:local' `
    '.'
if ($LASTEXITCODE -ne 0) {
    throw 'The OS2 API image build failed.'
}

Write-Host 'Replacing only advertified-os2-dev-api-1...'
Invoke-AdvertifiedCompose $composeFiles @(
    'up', '-d', '--no-build', '--no-deps', '--force-recreate', 'api'
)
Wait-AdvertifiedService $composeFiles 'api'
Assert-AdvertifiedComposeProject -RequireExisting

Write-Host 'Removing only dangling superseded image layers...'
& docker image prune --force | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to prune superseded dangling API layers.'
}
Invoke-AdvertifiedCompose $composeFiles @('ps', 'api')
