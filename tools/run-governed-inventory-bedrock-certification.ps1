$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repoRoot
$project = 'advertified-os2-dev'
$apiContainer = 'advertified-os2-dev-api-1'
$physicalReport = Join-Path $repoRoot 'artifacts\inventory-corpus\physical-certification\corpus-physical-certification.json'
$bedrockOverride = Join-Path $repoRoot 'artifacts\inventory-corpus\docker-compose.bedrock.override.yml'
$proOverride = Join-Path $repoRoot 'artifacts\inventory-corpus\docker-compose.bedrock-pro.override.yml'
$preflightOutput = Join-Path $repoRoot 'artifacts\inventory-corpus\ai-cost\bedrock-preflight.json'
$retryPlanPath = Join-Path $repoRoot 'artifacts\inventory-corpus\bedrock-certification\semantic-retry-plan.json'
$maximumInventoryCostMicros = 4311878
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

function Activate-Configuration {
    param([Parameter(Mandatory = $true)][string[]]$Files)
    Invoke-Checked -FilePath 'docker' -Arguments (
        Compose-Arguments -Files $Files -Command @('config', '--quiet')
    )
    Invoke-Checked -FilePath 'docker' -Arguments (
        Compose-Arguments -Files $Files -Command @(
            'up', '-d', '--no-build', '--force-recreate',
            'agent-runtime', 'api'
        )
    )
    Wait-Healthy 'http://127.0.0.1:5198/health/ready'
    Wait-Healthy 'http://127.0.0.1:5197/health/ready'
}

function Assert-PreflightBudget {
    param(
        [Parameter(Mandatory = $true)]$Preflight,
        [Parameter(Mandatory = $true)][string[]]$SelectedFiles
    )
    if (-not $Preflight.liveExecutionEnabled) {
        throw 'The governed Bedrock runtime did not activate.'
    }
    $sources = @($Preflight.sources)
    if ($SelectedFiles.Count -gt 0) {
        $selected = [System.Collections.Generic.HashSet[string]]::new(
            [string[]]$SelectedFiles,
            [System.StringComparer]::Ordinal
        )
        $sources = @($sources | Where-Object { $selected.Contains($_.fileName) })
        if ($sources.Count -ne $SelectedFiles.Count) {
            throw 'The retry preflight did not resolve every selected source.'
        }
    }
    $newMaximum = [int64](@($sources | Measure-Object `
        -Property newMaximumCostUsdMicros -Sum).Sum)
    $existing = [int64]$Preflight.existingCommittedCostUsdMicros
    if ($existing + $newMaximum -gt $maximumInventoryCostMicros) {
        throw 'Projected provider usage exceeds the US$4.311878 inventory allocation.'
    }
    $largest = [int64](@($sources | Measure-Object `
        -Property largestPacketCostUsdMicros -Maximum).Maximum)
    if ($largest -gt $maximumPerCallMicros) {
        throw 'At least one provider request exceeds the US$0.06 per-call ceiling.'
    }
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
        $_ -and
        $_ -notlike '*docker-compose.bedrock.override.yml' -and
        $_ -notlike '*docker-compose.bedrock-pro.override.yml' -and
        $_ -notlike '*docker-compose.canary.override.yml'
    }
if ($configFiles.Count -eq 0) {
    throw 'Unable to recover the existing OS2 Compose configuration.'
}
$liteFiles = @($configFiles) + @($bedrockOverride)

try {
    Write-Host 'Activating the governed Nova Lite classification pass...'
    Activate-Configuration -Files $liteFiles
    $preflight = Read-SemanticPreflight
    $preflight | ConvertTo-Json -Depth 100 | Set-Content `
        -Path $preflightOutput -Encoding utf8
    Assert-PreflightBudget -Preflight $preflight -SelectedFiles @()

    Invoke-Checked -FilePath 'python' -Arguments @(
        '.\tools\reproject_inventory_corpus.py',
        '--all', '--maximum', '43', '--max-wait-seconds', '1200'
    )

    & python '.\tools\certify_inventory_corpus_bedrock.py'
    $certificationExit = $LASTEXITCODE
    if ($certificationExit -ne 0) {
        Invoke-Checked -FilePath 'python' -Arguments @(
            '.\tools\plan_bedrock_semantic_retries.py'
        )
        $retryPlan = Get-Content $retryPlanPath -Raw | ConvertFrom-Json
        $retryFiles = @($retryPlan.retryDocuments | ForEach-Object {
            [string]$_.fileName
        })
        if ($retryFiles.Count -eq 0) {
            throw 'Bedrock certification failed without a safe semantic retry plan.'
        }

        Write-Host "Escalating $($retryFiles.Count) semantic-only documents to Nova Pro..."
        $proFiles = @($liteFiles) + @($proOverride)
        Activate-Configuration -Files $proFiles
        $proPreflight = Read-SemanticPreflight
        Assert-PreflightBudget -Preflight $proPreflight -SelectedFiles $retryFiles
        $arguments = @(
            '.\tools\reproject_inventory_corpus.py',
            '--maximum', [string]$retryFiles.Count,
            '--max-wait-seconds', '1200'
        )
        foreach ($file in $retryFiles) {
            $arguments += @('--document', $file)
        }
        Invoke-Checked -FilePath 'python' -Arguments $arguments
        Invoke-Checked -FilePath 'python' -Arguments @(
            '.\tools\certify_inventory_corpus_bedrock.py'
        )
    }

    Invoke-Checked -FilePath 'python' -Arguments @(
        '.\tools\report_inventory_ai_cost.py'
    )
    $completed = $true
}
finally {
    if (-not $completed) {
        Write-Warning 'Certification failed; reverting the same OS2 API/runtime to the non-live configuration.'
        try { Activate-Configuration -Files $configFiles }
        catch {
            Write-Error 'Automatic Bedrock rollback failed. Stop API/runtime before retrying.'
        }
    }
}

Write-Host 'All Bedrock outputs passed physical-baseline certification within the governed allocation.'
docker ps --filter "label=com.docker.compose.project=$project" `
    --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}'
