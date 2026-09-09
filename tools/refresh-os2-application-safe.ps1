$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'advertified-compose.ps1')
$composeFiles = @(
    'infrastructure/docker-compose.yml',
    'infrastructure/docker-compose.app.yml'
)

Push-Location $repoRoot
try {
    Assert-AdvertifiedComposeProject -RequireExisting

    Write-Host 'Building and replacing the agent runtime without dependencies or seeds...'
    Invoke-AdvertifiedCompose $composeFiles @('build', 'agent-runtime')
    Invoke-AdvertifiedCompose $composeFiles @(
        'up', '-d', '--no-build', '--no-deps', '--force-recreate', 'agent-runtime')
    Wait-AdvertifiedService $composeFiles 'agent-runtime'

    Write-Host 'Building and applying migrations without development seed execution...'
    Invoke-AdvertifiedCompose $composeFiles @('build', 'migrator')
    Invoke-AdvertifiedCompose $composeFiles @(
        'up', '--no-build', '--no-deps', '--force-recreate',
        '--abort-on-container-exit', '--exit-code-from', 'migrator', 'migrator')

    Write-Host 'Building and replacing the API without dependencies or seeds...'
    Invoke-AdvertifiedCompose $composeFiles @('build', 'api')
    Invoke-AdvertifiedCompose $composeFiles @(
        'up', '-d', '--no-build', '--no-deps', '--force-recreate', 'api')
    Wait-AdvertifiedService $composeFiles 'api'

    Write-Host 'Building and replacing the web service without dependencies or seeds...'
    Invoke-AdvertifiedCompose $composeFiles @('build', 'web')
    Invoke-AdvertifiedCompose $composeFiles @(
        'up', '-d', '--no-build', '--no-deps', '--force-recreate', 'web')
    Wait-AdvertifiedService $composeFiles 'web'

    Assert-AdvertifiedComposeProject -RequireExisting
    Invoke-AdvertifiedCompose $composeFiles @('ps')
}
finally {
    Pop-Location
}
