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

$postgresRows = @(
    & docker ps -a `
        --filter 'label=com.docker.compose.project=advertified-os2-dev' `
        --filter 'label=com.docker.compose.service=postgres' `
        --format '{{.ID}}|{{.Names}}|{{.State}}'
)
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to inspect OS2 Postgres task containers.'
}
$stalePostgresTasks = @(
    $postgresRows | ForEach-Object {
        $parts = $_ -split '\|', 3
        if (
            $parts.Count -eq 3 -and
            $parts[1] -ne 'advertified-os2-dev-postgres-1'
        ) {
            $parts[0]
        }
    }
)
if ($stalePostgresTasks.Count -gt 0) {
    Write-Host "Removing $($stalePostgresTasks.Count) noncanonical OS2 Postgres task containers..."
    & docker rm --force @stalePostgresTasks | Out-Host
    if ($LASTEXITCODE -ne 0) {
        Write-Warning (
            'At least one noncanonical OS2 task container could not be removed. ' +
            'The canonical advertified-os2-dev Postgres service is unchanged.'
        )
    }
}

function Wait-AdvertifiedMigrator {
    for ($attempt = 0; $attempt -lt 180; $attempt++) {
        $container = Get-AdvertifiedServiceContainer $composeFiles 'migrator'
        $state = & docker inspect --format '{{.State.Status}}|{{.State.ExitCode}}' `
            $container 2>$null
        if ($LASTEXITCODE -eq 0 -and $state -like 'exited|*') {
            $exitCode = [int](($state -split '\|', 2)[1])
            if ($exitCode -eq 0) { return }
            Invoke-AdvertifiedCompose $composeFiles @(
                'logs', '--tail', '120', 'migrator'
            )
            throw "The OS2 migrator exited with code $exitCode."
        }
        Start-Sleep -Seconds 1
    }
    Invoke-AdvertifiedCompose $composeFiles @(
        'logs', '--tail', '120', 'migrator'
    )
    throw 'The OS2 migrator did not complete.'
}

Assert-AdvertifiedComposeProject -RequireExisting
Write-Host 'Recreating the existing OS2 agent runtime with Bedrock disabled...'
Invoke-AdvertifiedCompose $composeFiles @(
    'up', '-d', '--no-build', '--no-deps', '--force-recreate', 'agent-runtime'
)
Wait-AdvertifiedService $composeFiles 'agent-runtime'

Write-Host 'Building the current migrator and API for the existing OS2 project...'
Invoke-AdvertifiedCompose $composeFiles @('build', 'migrator', 'api')

Write-Host 'Applying pending migrations with the existing OS2 migrator service...'
Invoke-AdvertifiedCompose $composeFiles @(
    'up', '-d', '--no-build', '--no-deps', '--force-recreate', 'migrator'
)
Wait-AdvertifiedMigrator

Write-Host 'Replacing only the existing OS2 API service...'
Invoke-AdvertifiedCompose $composeFiles @(
    'up', '-d', '--no-build', '--no-deps', 'api'
)
Wait-AdvertifiedService $composeFiles 'api'
Assert-AdvertifiedComposeProject -RequireExisting

Write-Host 'Removing only dangling superseded image layers...'
& docker image prune --force | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to prune superseded dangling image layers.'
}

Write-Host 'OS2 migration and API replacement are healthy.'
Invoke-AdvertifiedCompose $composeFiles @('ps', 'migrator', 'api')
