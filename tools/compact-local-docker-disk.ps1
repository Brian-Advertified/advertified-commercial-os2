param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
# Owner-authorised maintenance for this exact local Docker disk; never deletes or moves it.
$dockerDisk = 'C:\Users\CC KEMPTON\AppData\Local\Docker\wsl\disk\docker_data.vhdx'
$reportDirectory = Join-Path $PSScriptRoot '../.artifacts/storage-maintenance'
$reportPath = Join-Path $reportDirectory 'compaction-result.json'
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
$result = [ordered]@{ status = 'running'; beforeFreeBytes = (Get-PSDrive C).Free }
try {
    $principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Windows administrator access is required for Docker disk compaction.'
    }
    $resolved = (Resolve-Path -LiteralPath $dockerDisk).Path
    if ($resolved -ne $dockerDisk) { throw 'Unexpected Docker disk path.' }
    $disk = Get-VHD -Path $resolved
    if ($disk.Attached) { throw 'Docker disk is attached. Stop Docker Desktop and its WSL instance first.' }
    $result.beforeDiskBytes = (Get-Item -LiteralPath $resolved).Length
    Optimize-VHD -Path $resolved -Mode Full
    $result.afterDiskBytes = (Get-Item -LiteralPath $resolved).Length
    $result.afterFreeBytes = (Get-PSDrive C).Free
    $result.status = 'completed'
}
catch {
    $result.status = 'failed'
    $result.error = $_.Exception.Message
}
$result | ConvertTo-Json | Set-Content -LiteralPath $reportPath -Encoding UTF8
if ($result.status -ne 'completed') { exit 1 }
