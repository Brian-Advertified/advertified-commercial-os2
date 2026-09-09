$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptPath = (Resolve-Path (Join-Path $PSScriptRoot 'compact-local-docker-disk.ps1')).Path
$arguments = @(
    '-NoProfile',
    '-ExecutionPolicy', 'Bypass',
    '-File', ('"{0}"' -f $scriptPath)
)

Write-Host 'Requesting Windows administrator approval for Docker VHDX compaction...'
$process = Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList $arguments -Wait -PassThru
exit $process.ExitCode
