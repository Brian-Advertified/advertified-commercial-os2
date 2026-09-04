$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repoRoot
$project = 'advertified-os2-dev'
$apiContainer = 'advertified-os2-dev-api-1'
$publicationReport = Join-Path $repoRoot 'artifacts\inventory-corpus\publication\corpus-publication.json'
$costReport = Join-Path $repoRoot 'artifacts\inventory-corpus\ai-cost\inventory-ai-cost-report.json'
$canaryOverride = Join-Path $repoRoot 'artifacts\inventory-corpus\docker-compose.canary.override.yml'
$completed = $false

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath exited with code $LASTEXITCODE."
    }
}

function Compose-Arguments {
    param(
        [Parameter(Mandatory = $true)][string[]]$Files,
        [Parameter(Mandatory = $true)][string[]]$Command
    )
    $arguments = @('compose', '-p', $project)
    foreach ($file in $Files) { $arguments += @('-f', $file) }
    return $arguments + $Command
}

function Wait-Healthy {
    param([Parameter(Mandatory = $true)][string]$Uri)
    for ($attempt = 1; $attempt -le 90; $attempt++) {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $Uri -TimeoutSec 5
            if ($response.StatusCode -eq 200) { return }
        }
        catch { Start-Sleep -Seconds 2 }
    }
    throw "Health check did not pass: $Uri"
}

if (-not (Test-Path $publicationReport)) {
    throw 'The certified corpus publication report is missing.'
}
$publication = Get-Content $publicationReport -Raw | ConvertFrom-Json
if ($publication.verdict -ne 'PASS' -or $publication.sourceCount -ne 43) {
    throw 'All 43 sources must be published before the proposal canary.'
}
if (-not (Test-Path $costReport)) {
    throw 'The inventory AI cost report is missing.'
}
$cost = Get-Content $costReport -Raw | ConvertFrom-Json
if (-not $cost.passed -or [int64]$cost.remainingBudgetUsdMicros -lt 500000) {
    throw 'The required US$0.50 canary reserve is not available.'
}

$foreign = docker ps -a --format '{{.Names}}|{{.Label "com.docker.compose.project"}}' |
    Where-Object { $_ -match '^advertified-' } |
    Where-Object { ($_ -split '\|', 2)[1] -ne $project }
if ($foreign) {
    throw "Non-OS2 Advertified containers exist:`n$($foreign -join "`n")"
}

Invoke-Checked -FilePath 'python' -Arguments @(
    '.\tools\snapshot_ai_cost_baseline.py'
)

$labels = docker inspect $apiContainer --format '{{json .Config.Labels}}' |
    ConvertFrom-Json
$configFiles = @($labels.'com.docker.compose.project.config_files' -split ',') |
    ForEach-Object { $_.Trim() } |
    Where-Object {
        $_ -and
        $_ -notlike '*docker-compose.bedrock.override.yml' -and
        $_ -notlike '*docker-compose.canary.override.yml'
    }
if ($configFiles.Count -eq 0) {
    throw 'Unable to recover the existing OS2 Compose configuration.'
}
$liveFiles = @($configFiles) + @($canaryOverride)

try {
    Invoke-Checked -FilePath 'docker' -Arguments (
        Compose-Arguments -Files $liveFiles -Command @('config', '--quiet')
    )
    Invoke-Checked -FilePath 'docker' -Arguments (
        Compose-Arguments -Files $liveFiles -Command @(
            'up', '-d', '--no-build', '--force-recreate',
            'agent-runtime', 'api'
        )
    )
    Wait-Healthy 'http://127.0.0.1:5198/health/ready'
    Wait-Healthy 'http://127.0.0.1:5197/health/ready'
    Wait-Healthy 'http://127.0.0.1:3017/'

    Invoke-Checked -FilePath 'npm' -Arguments @(
        '--prefix', '.\web',
        'run', 'test:e2e:inventory-canary'
    )
    Invoke-Checked -FilePath 'python' -Arguments @(
        '.\tools\verify_brief_proposal_canary_cost.py'
    )
    $completed = $true
}
finally {
    if (-not $completed) {
        Write-Warning 'Canary failed; reverting the same OS2 API/runtime to the non-live base configuration.'
        try {
            Invoke-Checked -FilePath 'docker' -Arguments (
                Compose-Arguments -Files $configFiles -Command @(
                    'up', '-d', '--no-build', '--force-recreate',
                    'agent-runtime', 'api'
                )
            )
        }
        catch {
            Write-Error 'Automatic canary rollback failed. Stop API/runtime before retrying.'
        }
    }
}

Write-Host 'Published-inventory brief-to-proposal canary passed within the US$0.50 reserve.'
docker ps --filter "label=com.docker.compose.project=$project" `
    --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}'
