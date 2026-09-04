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

$migrator = Get-AdvertifiedServiceContainer $composeFiles 'migrator'
$migratorState = & docker inspect --format '{{.State.Status}}|{{.State.ExitCode}}' $migrator
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to inspect the OS2 migrator.'
}
$parts = $migratorState -split '\|', 2
if ($parts.Count -ne 2 -or $parts[0] -ne 'exited' -or [int]$parts[1] -ne 0) {
    throw "The OS2 migrator is not complete: $migratorState"
}

Write-Host 'Replacing only the existing OS2 API with the already-built image...'
Invoke-AdvertifiedCompose $composeFiles @(
    'up', '-d', '--no-build', '--no-deps', '--force-recreate', 'api'
)
Wait-AdvertifiedService $composeFiles 'api'
Assert-AdvertifiedComposeProject -RequireExisting

Write-Host 'Removing dangling superseded image layers...'
& docker image prune --force | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to prune superseded dangling image layers.'
}

Write-Host 'Existing OS2 API refresh completed.'
Invoke-AdvertifiedCompose $composeFiles @('ps', 'agent-runtime', 'migrator', 'api')
