param(
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$Filter,
    [switch]$Integration
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'storage-headroom.ps1')
# This verifier is intentionally memory-backed: source is read-only and all restore/build/test
# outputs live on tmpfs. Keep an emergency host floor, but do not require ordinary build headroom.
Assert-AdvertifiedStorageHeadroom -ExpectedGrowthBytes 0 -ReserveBytes 2GB
$hostFreeBefore = Get-AdvertifiedHostFreeBytes
$dockerfile = Get-Content -LiteralPath (Join-Path $repoRoot 'api/Dockerfile') -TotalCount 1
if ($dockerfile -notmatch '^FROM (mcr\.microsoft\.com/dotnet/sdk:10\.0\.400-[^ ]+@sha256:[a-f0-9]{64}) AS build$') {
    throw 'The canonical Docker-pinned SDK could not be resolved.'
}
$sdkImage = $Matches[1]
& docker image inspect $sdkImage *> $null
if ($LASTEXITCODE -ne 0) { throw 'The pinned SDK must already exist locally; this low-disk runner never pulls images.' }
# All compiler outputs and restored packages are disposable memory-backed files.
# The repository is read-only; no validation image, persistent volume or host SDK is used.
$arguments = @(
    'run', '--rm', '--pull=never', '--memory=3g', '--memory-swap=3g',
    '--tmpfs', '/work:rw,size=2g', '--tmpfs', '/tmp:rw,size=256m',
    '--mount', "type=bind,source=$repoRoot,target=/source,readonly",
    '--workdir', '/work',
    '-e', "ADVERTIFIED_TEST_FILTER=$Filter",
    '-e', 'NUGET_PACKAGES=/work/packages', '-e', 'DOTNET_CLI_HOME=/work/cli',
    '-e', 'DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1', '-e', 'DOTNET_CLI_TELEMETRY_OPTOUT=1',
    '-e', 'AWS_EC2_METADATA_DISABLED=true', '-e', 'ADVERTIFIED_AGENT_RUNTIME_MODE=deterministic'
)
if ($Integration) {
    $arguments += @('--mount', 'type=bind,source=/var/run/docker.sock,target=/var/run/docker.sock',
        '--add-host', 'host.docker.internal:host-gateway', '-e', 'TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal')
}
$arguments += @($sdkImage, 'bash', '/source/tools/run-api-memory-tests.sh')
& docker @arguments
if ($LASTEXITCODE -ne 0) { throw "Memory-backed Docker API verification failed with exit code $LASTEXITCODE." }
$hostFreeAfter = Get-AdvertifiedHostFreeBytes
$hostGrowth = [Math]::Max(0, $hostFreeBefore - $hostFreeAfter)
Write-Host ("Memory-backed verifier host growth: {0:N2} MiB." -f ($hostGrowth / 1MB))
if ($hostGrowth -gt 64MB) {
    throw 'Memory-backed verification unexpectedly consumed more than 64 MiB of host storage.'
}
