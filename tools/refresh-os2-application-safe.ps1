param(
    [ValidateSet('all', 'agent-runtime', 'migrator', 'api', 'web')]
    [string]$Service = 'all',
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'advertified-compose.ps1')
$composeFiles = @(
    'infrastructure/docker-compose.yml',
    'infrastructure/docker-compose.app.yml'
)

function Should-Refresh([string]$name) {
    return $Service -eq 'all' -or $Service -eq $name
}

function Prepare-Service([string]$name) {
    if ($SkipBuild) {
        Write-Host "Using the existing $name image; no build or seed execution."
        return
    }
    Write-Host "Building $name without dependencies or seeds..."
    Invoke-AdvertifiedCompose $composeFiles @('build', $name)
}

Push-Location $repoRoot
try {
    Assert-AdvertifiedComposeProject -RequireExisting

    if (Should-Refresh 'agent-runtime') {
        Prepare-Service 'agent-runtime'
        Invoke-AdvertifiedCompose $composeFiles @(
            'up', '-d', '--no-build', '--no-deps', '--force-recreate', 'agent-runtime')
        Wait-AdvertifiedService $composeFiles 'agent-runtime'
    }

    if (Should-Refresh 'migrator') {
        Prepare-Service 'migrator'
        Write-Host 'Applying migrations without development seed execution...'
        Invoke-AdvertifiedCompose $composeFiles @(
            'up', '--no-build', '--no-deps', '--force-recreate',
            '--abort-on-container-exit', '--exit-code-from', 'migrator', 'migrator')
    }

    if (Should-Refresh 'api') {
        Prepare-Service 'api'
        Invoke-AdvertifiedCompose $composeFiles @(
            'up', '-d', '--no-build', '--no-deps', '--force-recreate', 'api')
        Wait-AdvertifiedService $composeFiles 'api'
    }

    if (Should-Refresh 'web') {
        Prepare-Service 'web'
        Invoke-AdvertifiedCompose $composeFiles @(
            'up', '-d', '--no-build', '--no-deps', '--force-recreate', 'web')
        Wait-AdvertifiedService $composeFiles 'web'
    }

    Assert-AdvertifiedComposeProject -RequireExisting
    Invoke-AdvertifiedCompose $composeFiles @('ps')
}
finally {
    Pop-Location
}
