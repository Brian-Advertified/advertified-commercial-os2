$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repoRoot
$project = 'advertified-os2-dev'
$apiContainer = 'advertified-os2-dev-api-1'
$physicalReport = Join-Path $repoRoot 'artifacts\inventory-corpus\physical-certification\corpus-physical-certification.json'
$bedrockOverride = Join-Path $repoRoot 'artifacts\inventory-corpus\docker-compose.bedrock.override.yml'
$preflightOutput = Join-Path $repoRoot 'artifacts\inventory-corpus\ai-cost\bedrock-preflight.json'
$maximumNewCostMicros = 4811878
$maximumPerCallMicros = 60000
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
    foreach ($file in $Files) {
        $arguments += @('-f', $file)
    }
    return $arguments + $Command
}

function Wait-Healthy {
    param([Parameter(Mandatory = $true)][string]$Uri)
    for ($attempt = 1; $attempt -le 90; $attempt++) {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $Uri -TimeoutSec 5
            if ($response.StatusCode -eq 200) { return }
        }
        catch {
            Start-Sleep -Seconds 2
        }
    }
    throw "Health check did not pass: $Uri"
}

function Read-SemanticPreflight {
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $sessionView = Invoke-RestMethod -Uri 'http://127.0.0.1:5197/api/v1/session' `
        -Headers @{ Origin = 'http://localhost:3017' } -WebSession $session
    if (-not $sessionView.authenticated) {
        Invoke-RestMethod -Method Post `
            -Uri 'http://127.0.0.1:5197/api/v1/session' `
            -Headers @{
                Origin = 'http://localhost:3017'
                'X-CSRF-TOKEN' = $sessionView.antiforgeryToken
            } -WebSession $session | Out-Null
    }
    return Invoke-RestMethod `
        -Uri 'http://127.0.0.1:5197/api/v1/tenants/10000000-0000-0000-0000-000000000020/inventory-semantic-preflight' `
        -Headers @{ Origin = 'http://localhost:3017' } -WebSession $session
}

if (-not (Test-Path $physicalReport)) {
    throw 'The 43-file physical certification report is missing.'
}
$physical = Get-Content $physicalReport -Raw | ConvertFrom-Json
if ($physical.verdict -ne 'PASS' -or $physical.passedSourceCount -ne 43) {
    throw 'All 43 physical source files must pass before Bedrock is activated.'
}

$foreign = docker ps -a --format '{{.Names}}|{{.Label "com.docker.compose.project"}}' |
    Where-Object { $_ -match '^advertified-' } |
    Where-Object { ($_ -split '\|', 2)[1] -ne $project }
if ($foreign) {
    throw "Non-OS2 Advertified containers exist:`n$($foreign -join "`n")"
}

$labels = docker inspect $apiContainer --format '{{json .Config.Labels}}' |
    ConvertFrom-Json
$configFiles = @($labels.'com.docker.compose.project.config_files' -split ',') |
    ForEach-Object { $_.Trim() } |
    Where-Object {
        $_ -and $_ -notlike '*docker-compose.bedrock.override.yml'
    }
if ($configFiles.Count -eq 0) {
    throw 'Unable to recover the existing OS2 Compose configuration.'
}
$liveFiles = @($configFiles) + @($bedrockOverride)

try {
    Write-Host 'Validating the governed Bedrock configuration...'
    Invoke-Checked -FilePath 'docker' -Arguments (
        Compose-Arguments -Files $liveFiles -Command @('config', '--quiet')
    )

    Write-Host 'Recreating only the existing OS2 API and agent-runtime with governed Bedrock enabled...'
    Invoke-Checked -FilePath 'docker' -Arguments (
        Compose-Arguments -Files $liveFiles -Command @(
            'up', '-d', '--no-build', '--force-recreate',
            'agent-runtime', 'api'
        )
    )
    Wait-Healthy 'http://127.0.0.1:5198/health/ready'
    Wait-Healthy 'http://127.0.0.1:5197/health/ready'

    $preflight = Read-SemanticPreflight
    $preflight | ConvertTo-Json -Depth 100 | Set-Content `
        -Path $preflightOutput -Encoding utf8
    if (-not $preflight.liveExecutionEnabled) {
        throw 'The governed Bedrock runtime did not activate.'
    }
    if ([int64]$preflight.newMaximumCostUsdMicros -gt $maximumNewCostMicros) {
        throw "Projected corpus cost exceeds the remaining US$4.811878 budget."
    }
    $largest = @($preflight.sources | ForEach-Object {
        [int64]$_.largestPacketCostUsdMicros
    } | Measure-Object -Maximum).Maximum
    if ([int64]$largest -gt $maximumPerCallMicros) {
        throw 'At least one request exceeds the US$0.06 per-call ceiling.'
    }

    Write-Host 'Running semantic enrichment for all physically certified files...'
    Invoke-Checked -FilePath 'python' -Arguments @(
        '.\tools\reproject_inventory_corpus.py',
        '--all',
        '--maximum', '43',
        '--max-wait-seconds', '1200'
    )

    Write-Host 'Comparing every Bedrock result to its physical baseline...'
    Invoke-Checked -FilePath 'python' -Arguments @(
        '.\tools\certify_inventory_corpus_bedrock.py'
    )
    Invoke-Checked -FilePath 'python' -Arguments @(
        '.\tools\report_inventory_ai_cost.py'
    )
    $completed = $true
}
finally {
    if (-not $completed) {
        Write-Warning 'Certification failed; reverting the same OS2 API/runtime to the non-live configuration.'
        try {
            Invoke-Checked -FilePath 'docker' -Arguments (
                Compose-Arguments -Files $configFiles -Command @(
                    'up', '-d', '--no-build', '--force-recreate',
                    'agent-runtime', 'api'
                )
            )
        }
        catch {
            Write-Error 'Automatic Bedrock rollback failed. Stop API/runtime before any retry.'
        }
    }
}

Write-Host 'All Bedrock outputs passed physical-baseline certification within the US$5 ceiling.'
docker ps --filter "label=com.docker.compose.project=$project" `
    --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}'
