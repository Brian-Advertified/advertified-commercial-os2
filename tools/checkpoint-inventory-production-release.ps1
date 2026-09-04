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

$runtime = Invoke-AdvertifiedCompose $composeFiles @(
    'exec', '-T', 'agent-runtime',
    'python', '-c', "import os; print(os.getenv('ADVERTIFIED_AGENT_RUNTIME_MODE', ''))"
)
if ($LASTEXITCODE -ne 0 -or $runtime.Trim() -ne 'deterministic') {
    throw 'The agent runtime is not in deterministic zero-cost mode.'
}

& python .\tools\generate_inventory_production_release_register.py
if ($LASTEXITCODE -ne 0) {
    throw 'The software production-release gate did not pass.'
}

$registerPath = Join-Path $repo `
    'artifacts\inventory-corpus\production-release\corpus-release-register.json'
$register = Get-Content $registerPath -Raw | ConvertFrom-Json

if ($register.softwareLaunchGate -ne 'GO') {
    throw 'The software launch gate is not GO.'
}
if ($register.corpusPublicationGate -ne 'NO_GO') {
    throw 'Unexpected corpus publication state. Uncertified inventory must remain quarantined.'
}
if ([int]$register.summary.sourceCount -ne 43) {
    throw 'The production register does not contain all 43 corpus files.'
}
if ([int]$register.summary.certifiedSourceCount -ne 1) {
    throw 'The production register must contain exactly one physically certified source at this checkpoint.'
}
if ([int]$register.summary.quarantinedSourceCount -ne 42) {
    throw 'The production register must quarantine the remaining 42 sources.'
}
if ([int]$register.summary.publishedCandidateCount -ne 0) {
    throw 'The production checkpoint found published corpus candidates.'
}
if ([bool]$register.bedrock.liveExecutionEnabled) {
    throw 'Live Bedrock execution is enabled.'
}
if ([long]$register.bedrock.committedCostUsdMicros -ne 0) {
    throw 'The active production checkpoint contains committed Bedrock cost.'
}

& git diff --check
if ($LASTEXITCODE -ne 0) {
    throw 'Git diff validation failed.'
}

& git add -A
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to stage the production checkpoint.'
}

& git diff --cached --quiet
if ($LASTEXITCODE -eq 0) {
    Write-Host 'No uncommitted production-checkpoint changes remain.'
} else {
    & git commit -m 'feat(inventory): establish production-safe corpus release boundary'
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to commit the production checkpoint.'
    }
}

$head = (& git rev-parse HEAD).Trim()
$status = & git status --short
if ($status) {
    throw 'The repository is not clean after the production checkpoint.'
}

Write-Host ''
Write-Host 'Inventory production-safe checkpoint complete.'
Write-Host "Commit: $head"
Write-Host 'Software launch gate: GO'
Write-Host 'Corpus publication gate: NO_GO (42 sources quarantined)'
Write-Host 'Bedrock: disabled; active-scope committed cost US$0.00'
