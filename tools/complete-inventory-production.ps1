$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repoRoot
$outputRoot = Join-Path $repoRoot 'artifacts\inventory-corpus\completion'
$validationPath = Join-Path $outputRoot 'validation-steps.json'
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$steps = [System.Collections.Generic.List[object]]::new()

function Save-Validation {
    param([Parameter(Mandatory = $true)][string]$Verdict)
    $payload = [ordered]@{
        schemaVersion = 'advertified.inventory-final-validation.v1'
        generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        verdict = $Verdict
        stepCount = $steps.Count
        passedStepCount = @($steps | Where-Object { $_.passed }).Count
        steps = @($steps)
    }
    $payload | ConvertTo-Json -Depth 20 | Set-Content `
        -Path $validationPath -Encoding utf8
}

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [string]$WorkingDirectory = $repoRoot
    )
    Write-Host "`n=== $Name ==="
    $started = [DateTimeOffset]::UtcNow
    Push-Location $WorkingDirectory
    try {
        & $FilePath @Arguments
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
    $completed = [DateTimeOffset]::UtcNow
    $passed = $exitCode -eq 0
    $steps.Add([ordered]@{
        name = $Name
        passed = $passed
        exitCode = $exitCode
        startedAtUtc = $started.ToString('O')
        completedAtUtc = $completed.ToString('O')
        durationSeconds = [Math]::Round(($completed - $started).TotalSeconds, 3)
    })
    Save-Validation -Verdict $(if ($passed) { 'RUNNING' } else { 'FAIL' })
    if (-not $passed) {
        throw "$Name failed with exit code $exitCode."
    }
}

try {
    $foreign = docker ps -a --format '{{.Names}}|{{.Label "com.docker.compose.project"}}' |
        Where-Object { $_ -match '^advertified-' } |
        Where-Object { ($_ -split '\|', 2)[1] -ne 'advertified-os2-dev' }
    if ($foreign) {
        throw "Non-OS2 Advertified containers exist:`n$($foreign -join "`n")"
    }

    Invoke-Step 'Generate governed pricing alias' 'python' @(
        '.\tools\generate_inventory_pricing_alias.py'
    )
    Invoke-Step 'Enable pending supplier publication contract' 'python' @(
        '.\tools\enable_pending_supplier_publication.py'
    )
    Invoke-Step 'API Release build' 'dotnet' @(
        'build', '.\api\Advertified.Commercial.Api.csproj',
        '-c', 'Release', '--no-restore'
    )
    Invoke-Step 'Complete API test suite' 'dotnet' @(
        'test',
        '.\api\tests\Advertified.Commercial.Api.Tests\Advertified.Commercial.Api.Tests.csproj',
        '-c', 'Release', '--no-restore'
    )
    Invoke-Step 'Agent runtime test suite' 'python' @(
        '-m', 'pytest', '-q'
    ) (Join-Path $repoRoot 'agent-runtime')
    Invoke-Step 'Corpus, certification and architecture tests' 'python' @(
        '-m', 'pytest',
        'tests/test_inventory_physical_certification.py',
        'tests/test_inventory_ai_cost_ledger.py',
        'tests/test_inventory_file_gold.py',
        'tests/test_inventory_corpus_tools.py',
        'tests/architecture/test_boundaries.py',
        '-q'
    )
    Invoke-Step 'Generate inventory AI budget UI state' 'python' @(
        '.\tools\generate_inventory_ai_budget_ui_state.py'
    )
    Invoke-Step 'Web production build' 'npm' @(
        '--prefix', '.\web', 'run', 'build'
    )
    Invoke-Step 'Web lint' 'npm' @(
        '--prefix', '.\web', 'run', 'lint'
    )
    Invoke-Step 'Web unit tests' 'npm' @(
        '--prefix', '.\web', 'run', 'test'
    )

    Invoke-Step 'Physical certification of all 43 sources' 'powershell' @(
        '-ExecutionPolicy', 'Bypass',
        '-File', '.\tools\complete-inventory-physical-certification.ps1'
    )
    Invoke-Step 'Governed Bedrock request and response certification' 'powershell' @(
        '-ExecutionPolicy', 'Bypass',
        '-File', '.\tools\run-governed-inventory-bedrock-certification.ps1'
    )
    Invoke-Step 'Inventory AI cost reconciliation' 'python' @(
        '.\tools\report_inventory_ai_cost.py'
    )
    Invoke-Step 'Pending supplier rate schema' 'powershell' @(
        '-ExecutionPolicy', 'Bypass',
        '-File', '.\tools\apply-inventory-pending-rate-migration.ps1'
    )
    Invoke-Step 'Publication readiness audit' 'python' @(
        '.\tools\audit_inventory_publication_readiness.py'
    )
    Invoke-Step 'Publication API dry run' 'python' @(
        '.\tools\publish_certified_inventory.py', '--dry-run'
    )
    Invoke-Step 'Publish all certified inventory' 'python' @(
        '.\tools\publish_certified_inventory.py'
    )
    Invoke-Step 'Published inventory brief-to-proposal canary' 'powershell' @(
        '-ExecutionPolicy', 'Bypass',
        '-File', '.\tools\run-published-inventory-brief-proposal-canary.ps1'
    )
    Invoke-Step 'Generate final inventory AI budget UI state' 'python' @(
        '.\tools\generate_inventory_ai_budget_ui_state.py'
    )
    Invoke-Step 'Update existing OS2 inventory UI' 'powershell' @(
        '-ExecutionPolicy', 'Bypass',
        '-File', '.\tools\update-os2-web-safe.ps1'
    )

    Save-Validation -Verdict 'PASS'
    Invoke-Step 'Generate final production completion report' 'python' @(
        '.\tools\generate_inventory_completion_report.py',
        '--validation', $validationPath
    )
    Save-Validation -Verdict 'PASS'
}
catch {
    Save-Validation -Verdict 'FAIL'
    throw
}

Write-Host '`nInventory production completion passed.'
