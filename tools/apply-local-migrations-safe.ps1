$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'advertified-compose.ps1')
$composeFiles = @('infrastructure/docker-compose.yml', 'infrastructure/docker-compose.app.yml')

Push-Location $repoRoot
try {
    Assert-AdvertifiedComposeProject -RequireExisting
    Assert-AdvertifiedStorageHeadroom -ExpectedGrowthBytes 0
    Invoke-AdvertifiedCompose $composeFiles @('run', '--rm', '--no-deps', 'migrator', '--apply')
    Assert-AdvertifiedComposeProject -RequireExisting
}
finally {
    Pop-Location
}
