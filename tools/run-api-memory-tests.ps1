param(
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$Filter,
    [switch]$Integration,
    [switch]$PartitionByCategory,
    [ValidatePattern('^[a-z0-9-]{1,64}$')][string]$EvidenceName
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'storage-headroom.ps1')
# Compilation and restore stay memory-backed; retained test receipts use a small explicit directory.
Assert-AdvertifiedStorageHeadroom -ExpectedGrowthBytes 0 -ReserveBytes 2GB
$hostFreeBefore = Get-AdvertifiedHostFreeBytes
$dockerfile = Get-Content -LiteralPath (Join-Path $repoRoot 'api/Dockerfile') -TotalCount 1
if ($dockerfile -notmatch '^FROM (mcr\.microsoft\.com/dotnet/sdk:10\.0\.400-[^ ]+@sha256:[a-f0-9]{64}) AS build$') {
    throw 'The canonical Docker-pinned SDK could not be resolved.'
}
$sdkImage = $Matches[1]
& docker image inspect $sdkImage *> $null
if ($LASTEXITCODE -ne 0) { throw 'The pinned SDK must already exist locally; this runner never pulls images.' }
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
if ($PartitionByCategory) {
    if (-not $EvidenceName) { throw 'Partitioned verification requires retained evidence.' }
    $arguments += @('-e', 'ADVERTIFIED_TEST_PARTITION_BY_CATEGORY=1')
}
if ($Integration) {
    $arguments += @('--mount', 'type=bind,source=/var/run/docker.sock,target=/var/run/docker.sock',
        '--add-host', 'host.docker.internal:host-gateway', '-e', 'TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal')
}
$evidencePath = $null
if ($EvidenceName) {
    $evidencePath = Join-Path $repoRoot "artifacts/backend-production-completion/$EvidenceName"
    if (Test-Path -LiteralPath $evidencePath) {
        $existing = @(Get-ChildItem -LiteralPath $evidencePath -Force)
        if ($existing.Count -gt 0) { throw 'Evidence names must be unique; existing receipts are never overwritten.' }
    }
    else {
        New-Item -ItemType Directory -Path $evidencePath -Force | Out-Null
    }
    $arguments += @('--mount', "type=bind,source=$evidencePath,target=/evidence",
        '-e', 'ADVERTIFIED_TEST_EVIDENCE_DIRECTORY=/evidence')
}
$arguments += @($sdkImage, 'bash', '/source/tools/run-api-memory-tests.sh')
$verificationExitCode = -1
$postStorage = $null
try {
    & docker @arguments
    $verificationExitCode = $LASTEXITCODE
}
finally {
    $hostFreeAfter = Get-AdvertifiedHostFreeBytes
    $hostGrowth = [Math]::Max(0, $hostFreeBefore - $hostFreeAfter)
    Write-Host ("Verifier host free bytes: {0} -> {1}; host-wide movement {2:N2} MiB." -f
        $hostFreeBefore, $hostFreeAfter, ($hostGrowth / 1MB))
    $postStorageArguments = @{
        HostFreeBefore = $hostFreeBefore
        HostFreeAfter = $hostFreeAfter
        EvidencePath = $evidencePath
        MaximumRetainedEvidenceBytes = 64MB
        ReserveBytes = 2GB
    }
    $postStorage = Assert-AdvertifiedPostOperationStorage @postStorageArguments
    if ($evidencePath) {
        @{ filter = $Filter; integration = [bool]$Integration; partitionByCategory = [bool]$PartitionByCategory;
            sdkImage = $sdkImage; exitCode = $verificationExitCode; hostFreeBefore = $hostFreeBefore;
            hostFreeAfter = $hostFreeAfter; hostGrowthBytes = $postStorage.HostGrowthBytes;
            retainedEvidenceBytes = $postStorage.RetainedEvidenceBytes;
            hostGrowthAttributedToVerifier = $postStorage.HostGrowthAttributedToVerifier;
            utc = [DateTime]::UtcNow.ToString('o') } |
            ConvertTo-Json | Set-Content -LiteralPath (Join-Path $evidencePath 'run.json') -Encoding UTF8
        Write-Host "Retained API evidence: $evidencePath"
    }
}
if ($verificationExitCode -ne 0) { throw "Memory-backed Docker API verification failed with exit code $verificationExitCode." }
if ($PartitionByCategory) {
    & python -B (Join-Path $PSScriptRoot 'reconcile-api-partitions.py') $evidencePath
    if ($LASTEXITCODE -ne 0) { throw 'API partition discovery and receipts do not reconcile.' }
}
