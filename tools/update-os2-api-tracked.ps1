$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$statusRoot = Join-Path $repoRoot 'artifacts\inventory-corpus\operations'
$statusPath = Join-Path $statusRoot 'os2-api-refresh.json'
New-Item -ItemType Directory -Force -Path $statusRoot | Out-Null
$started = [DateTimeOffset]::UtcNow

function Write-Status {
    param(
        [Parameter(Mandatory = $true)][string]$State,
        [string]$Message = '',
        [string]$ContainerId = ''
    )
    [ordered]@{
        schemaVersion = 'advertified.os2-api-refresh.v1'
        state = $State
        startedAtUtc = $started.ToString('O')
        updatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        message = $Message
        composeProject = 'advertified-os2-dev'
        apiContainerId = $ContainerId
    } | ConvertTo-Json -Depth 10 | Set-Content -Path $statusPath -Encoding utf8
}

Write-Status -State 'RUNNING' -Message 'Building existing OS2 migrator and API.'
try {
    & powershell -ExecutionPolicy Bypass -File `
        (Join-Path $PSScriptRoot 'update-os2-api-only.ps1')
    if ($LASTEXITCODE -ne 0) {
        throw "OS2 API update exited with code $LASTEXITCODE."
    }
    $container = docker ps --filter `
        'label=com.docker.compose.project=advertified-os2-dev' `
        --filter 'label=com.docker.compose.service=api' `
        --format '{{.ID}}'
    if ($LASTEXITCODE -ne 0 -or @($container).Count -ne 1) {
        throw 'Unable to identify the single OS2 API container.'
    }
    Write-Status -State 'PASS' -Message 'Existing OS2 API is healthy.' `
        -ContainerId $container
}
catch {
    Write-Status -State 'FAIL' -Message $_.Exception.Message
    throw
}
