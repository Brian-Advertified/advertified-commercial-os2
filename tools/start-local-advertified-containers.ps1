param(
    [switch]$BackendOnly,
    [switch]$IncludeOptionalInfrastructure
)

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

    $entryService = if ($BackendOnly) { 'api' } else { 'web' }
    Invoke-AdvertifiedCompose $composeFiles @('up', '--detach', '--no-build', $entryService)

    if ($IncludeOptionalInfrastructure) {
        Invoke-AdvertifiedCompose $composeFiles @(
            'up', '--detach', '--no-build', '--no-deps', 'redis', 'mailhog')
    }

    $required = if ($BackendOnly) {
        @('postgres', 'agent-runtime', 'minio', 'api')
    }
    else {
        @('postgres', 'agent-runtime', 'minio', 'api', 'web')
    }

    foreach ($service in $required) {
        Wait-AdvertifiedService $composeFiles $service
    }

    Assert-AdvertifiedComposeProject -RequireExisting
    Invoke-AdvertifiedCompose $composeFiles @('ps')

    Write-Host 'Advertified local Compose startup completed in the canonical advertified-os2-dev project.'
    Write-Host 'The retained migrator and bootstrap/seed services may be Exited (0); they are one-shot startup jobs, not runtime services.'
    if (-not $IncludeOptionalInfrastructure) {
        Write-Host 'Redis and Mailhog were not started because they are optional development utilities.'
    }
}
finally {
    Pop-Location
}
