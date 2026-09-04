param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repo

$gate = Join-Path $repo 'artifacts\inventory-corpus\certification\inventory-upload-verification.json'
if (-not (Test-Path $gate)) {
    throw 'Inventory upload verification is missing.'
}
$upload = Get-Content $gate -Raw | ConvertFrom-Json
if (-not $upload.passed -or [int]$upload.publishedCandidateCount -le 0) {
    throw 'Brief-to-proposal testing is fenced until certified inventory upload passes.'
}

$projects = @(
    docker ps -a --format '{{.Label "com.docker.compose.project"}}' |
        Where-Object { $_ -and $_ -like 'advertified*' } |
        Sort-Object -Unique
)
if ($projects.Count -ne 1 -or $projects[0] -ne 'advertified-os2-dev') {
    throw "Expected only advertified-os2-dev; found: $($projects -join ', ')"
}

$required = @(
    'advertified-os2-dev-web-1',
    'advertified-os2-dev-api-1',
    'advertified-os2-dev-agent-runtime-1'
)
foreach ($container in $required) {
    $health = docker inspect --format '{{.State.Health.Status}}' $container 2>$null
    if ($LASTEXITCODE -ne 0 -or $health -ne 'healthy') {
        throw "$container is not healthy."
    }
}

Push-Location (Join-Path $repo 'web')
try {
    npx playwright test --config=playwright.connected.config.ts
    if ($LASTEXITCODE -ne 0) {
        throw 'Connected brief-to-proposal Playwright verification failed.'
    }
}
finally {
    Pop-Location
}

python .\tools\verify_brief_to_proposal_inventory.py
if ($LASTEXITCODE -ne 0) {
    throw 'The proposal did not prove use of certified corpus inventory.'
}

Write-Host 'Certified inventory brief-to-proposal verification passed.'
