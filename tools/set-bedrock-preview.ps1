param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Enable', 'Disable')]
    [string]$Mode,
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$')]
    [string]$AwsProfile = 'advertified-codex-audit'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'advertified-compose.ps1')
$composeFiles = @('infrastructure/docker-compose.yml', 'infrastructure/docker-compose.app.yml')

Push-Location $repoRoot
try {
    Assert-AdvertifiedComposeProject -RequireExisting
    Assert-AdvertifiedStorageHeadroom -ExpectedGrowthBytes 0
    if ($Mode -eq 'Enable') {
        # This only checks headroom. Every provider dispatch still reserves atomically in the API.
        & python (Join-Path $PSScriptRoot 'bedrock_preview_cost.py') guard
        if ($LASTEXITCODE -ne 0) { throw 'The existing owner AI budget has insufficient verified headroom.' }
        & aws sts get-caller-identity --profile $AwsProfile --query Account --output text
        if ($LASTEXITCODE -ne 0) { throw 'Refresh the authorised AWS SSO session before enabling preview.' }
        $env:AWS_PROFILE = $AwsProfile
        $composeFiles += 'infrastructure/development/docker-compose.bedrock-preview.yml'
    }
    # Recreate only existing application services. Never restart migrations/seeds or remove containers.
    Invoke-AdvertifiedCompose $composeFiles @('config', '--quiet')
    Invoke-AdvertifiedCompose $composeFiles @(
        'up', '--detach', '--no-build', '--no-deps', '--force-recreate', 'agent-runtime', 'api')
    Wait-AdvertifiedService $composeFiles 'agent-runtime'
    Wait-AdvertifiedService $composeFiles 'api'
    Wait-AdvertifiedService $composeFiles 'web'
    Assert-AdvertifiedComposeProject -RequireExisting
    Write-Output "Advertified preview runtime mode: $Mode"
}
finally {
    Pop-Location
}
