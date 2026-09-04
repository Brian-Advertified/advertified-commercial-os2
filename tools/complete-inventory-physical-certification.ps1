$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repoRoot

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

Write-Host 'Confirming that Advertified uses only the advertified-os2-dev Compose project...'
$advertifiedContainers = docker ps -a --format '{{.Names}}|{{.Label "com.docker.compose.project"}}' |
    Where-Object { $_ -match '^advertified-' }
$foreign = $advertifiedContainers | Where-Object {
    ($_ -split '\|', 2)[1] -ne 'advertified-os2-dev'
}
if ($foreign) {
    throw "Non-OS2 Advertified containers exist:`n$($foreign -join "`n")"
}

Write-Host 'Verifying physical-certification mode before any corpus mutation...'
$preflight = Invoke-RestMethod -Uri 'http://127.0.0.1:5197/api/v1/session' `
    -Headers @{ Origin = 'http://localhost:3017' } -SessionVariable session
if (-not $preflight.authenticated) {
    Invoke-RestMethod -Method Post -Uri 'http://127.0.0.1:5197/api/v1/session' `
        -Headers @{
            Origin = 'http://localhost:3017'
            'X-CSRF-TOKEN' = $preflight.antiforgeryToken
        } -WebSession $session | Out-Null
}
$semantic = Invoke-RestMethod `
    -Uri 'http://127.0.0.1:5197/api/v1/tenants/10000000-0000-0000-0000-000000000020/inventory-semantic-preflight' `
    -Headers @{ Origin = 'http://localhost:3017' } -WebSession $session
if ($semantic.liveExecutionEnabled) {
    throw 'Live Bedrock must remain disabled until physical certification passes.'
}

Write-Host 'Building and replacing only the existing OS2 API, then revalidating DMS...'
Invoke-Checked -FilePath 'powershell' -Arguments @(
    '-ExecutionPolicy', 'Bypass',
    '-File', '.\tools\update-os2-api-and-repair-dms.ps1'
)

Write-Host 'Reprojecting all 43 retained physical sources through the existing OS2 API...'
Invoke-Checked -FilePath 'python' -Arguments @(
    '.\tools\reproject_inventory_corpus.py',
    '--all',
    '--maximum', '43',
    '--max-wait-seconds', '900'
)

Write-Host 'Running independent source-map-to-candidate physical certification...'
Invoke-Checked -FilePath 'python' -Arguments @(
    '.\tools\certify_inventory_corpus_physical.py',
    '--promote'
)

Write-Host 'Physical certification completed. No Bedrock call was made.'
docker ps --filter 'label=com.docker.compose.project=advertified-os2-dev' `
    --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}'
docker system df
