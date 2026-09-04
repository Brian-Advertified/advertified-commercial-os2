$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo
. (Join-Path $PSScriptRoot 'advertified-compose.ps1')

$composeFiles = @(
    'infrastructure/docker-compose.yml',
    'infrastructure/docker-compose.app.yml',
    'artifacts/inventory-corpus/docker-compose.override.yml'
)

Assert-AdvertifiedComposeProject -RequireExisting
Write-Host 'Building the current API release candidate for the existing OS2 project...'
Invoke-AdvertifiedCompose $composeFiles @('build', 'api')
Invoke-AdvertifiedCompose $composeFiles @(
    'up', '-d', '--no-build', '--no-deps', 'api'
)
Wait-AdvertifiedService $composeFiles 'api'
Assert-AdvertifiedComposeProject -RequireExisting
Write-Host 'OS2 API replacement is healthy.'
Invoke-AdvertifiedCompose $composeFiles @('ps', 'api')
