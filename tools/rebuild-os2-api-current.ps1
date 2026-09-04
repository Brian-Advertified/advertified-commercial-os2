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
$expectedProjection = 'advertified-projection/3.5.0'
$tenantId = '10000000-0000-0000-0000-000000000020'
$origin = 'http://localhost:3017'
$apiBase = 'http://127.0.0.1:5197'

Assert-AdvertifiedComposeProject -RequireExisting
Write-Host 'Building only the existing OS2 API image from the current source...'
Invoke-AdvertifiedCompose $composeFiles @('build', 'api')

Write-Host 'Replacing only the existing OS2 API container...'
Invoke-AdvertifiedCompose $composeFiles @(
    'up', '-d', '--no-build', '--no-deps', '--force-recreate', 'api'
)
Wait-AdvertifiedService $composeFiles 'api'
Assert-AdvertifiedComposeProject -RequireExisting

$session = Invoke-RestMethod -Uri "$apiBase/api/v1/session" `
    -Headers @{ Origin = $origin } -SessionVariable webSession
if (-not $session.authenticated) {
    Invoke-RestMethod -Method Post -Uri "$apiBase/api/v1/session" `
        -Headers @{
            Origin = $origin
            'X-CSRF-TOKEN' = $session.antiforgeryToken
        } -WebSession $webSession | Out-Null
}
$preflight = Invoke-RestMethod `
    -Uri "$apiBase/api/v1/tenants/$tenantId/inventory-semantic-preflight" `
    -Headers @{ Origin = $origin } -WebSession $webSession
if ($preflight.liveExecutionEnabled) {
    throw 'The refreshed API unexpectedly enabled live Bedrock execution.'
}
if ($preflight.projectionVersion -notlike "*$expectedProjection*") {
    throw "The refreshed API is not running $expectedProjection."
}
if ([int64]$preflight.existingCommittedCostUsdMicros -ne 0) {
    throw 'The physical-validation scope has committed Bedrock cost.'
}

Write-Host 'Removing dangling superseded image layers...'
& docker image prune --force | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to prune superseded dangling image layers.'
}

Write-Host 'OS2 API is healthy on the current physical-validation projection.'
[ordered]@{
    composeProject = 'advertified-os2-dev'
    projectionVersion = $preflight.projectionVersion
    bedrockLiveExecutionEnabled = [bool]$preflight.liveExecutionEnabled
    bedrockCommittedCostUsdMicros = [int64]$preflight.existingCommittedCostUsdMicros
} | ConvertTo-Json -Depth 5
