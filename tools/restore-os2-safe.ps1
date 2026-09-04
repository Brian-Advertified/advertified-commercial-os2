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

Write-Host 'Building and replacing the OS2 agent runtime only...'
Invoke-AdvertifiedCompose $composeFiles @('build', 'agent-runtime')
Invoke-AdvertifiedCompose $composeFiles @('up', '-d', '--no-build', '--no-deps', 'agent-runtime')
Wait-AdvertifiedService $composeFiles 'agent-runtime'

Write-Host 'Building and applying the current database migrations...'
Invoke-AdvertifiedCompose $composeFiles @('build', 'migrator')
Invoke-AdvertifiedCompose $composeFiles @('up', '--no-build', '--no-deps', 'migrator')

Write-Host 'Building and replacing the OS2 API only...'
Invoke-AdvertifiedCompose $composeFiles @('build', 'api')
Invoke-AdvertifiedCompose $composeFiles @('up', '-d', '--no-build', '--no-deps', 'api')
Wait-AdvertifiedService $composeFiles 'api'

Write-Host 'Building and restoring the OS2 web service on port 3017...'
Invoke-AdvertifiedCompose $composeFiles @('build', 'web')
Invoke-AdvertifiedCompose $composeFiles @('up', '-d', '--no-build', '--no-deps', 'web')
Wait-AdvertifiedService $composeFiles 'web'

Assert-AdvertifiedComposeProject -RequireExisting

Write-Host ''
Write-Host 'OS2 restored in deterministic mode. Live Bedrock processing is disabled.'
Invoke-AdvertifiedCompose $composeFiles @('ps')
& docker system df
