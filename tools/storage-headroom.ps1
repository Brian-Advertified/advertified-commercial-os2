param([switch]$Report)

# Host reserve is operational safety headroom, not a promise about other applications.
function Get-AdvertifiedHostFreeBytes {
    $workspacePath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
    $drive = [System.IO.DriveInfo]::new([System.IO.Path]::GetPathRoot($workspacePath))
    return $drive.AvailableFreeSpace
}

function Get-AdvertifiedPathSizeBytes {
    param([AllowNull()][string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path)) {
        return [long]0
    }

    $total = [long]0
    Get-ChildItem -LiteralPath $Path -File -Recurse -Force | ForEach-Object {
        $total += [long]$_.Length
    }
    return $total
}

function Assert-AdvertifiedStorageHeadroom {
    param([long]$ExpectedGrowthBytes = 2GB, [long]$ReserveBytes = 5GB)
    if ($ExpectedGrowthBytes -lt 0 -or $ReserveBytes -lt 0) {
        throw 'Storage headroom values cannot be negative.'
    }
    $available = Get-AdvertifiedHostFreeBytes
    Write-Host ("Host storage: {0:N2} GiB free; {1:N2} GiB reserve; {2:N2} GiB operation allowance." -f
        ($available / 1GB), ($ReserveBytes / 1GB), ($ExpectedGrowthBytes / 1GB))
    if ($available -lt ($ReserveBytes + $ExpectedGrowthBytes)) {
        throw 'Insufficient host storage headroom for this operation; retain the reserve and reclaim only disposable build/test artifacts.'
    }
}

function Assert-AdvertifiedPostOperationStorage {
    param(
        [Parameter(Mandatory = $true)][long]$HostFreeBefore,
        [Parameter(Mandatory = $true)][long]$HostFreeAfter,
        [AllowNull()][string]$EvidencePath,
        [long]$MaximumRetainedEvidenceBytes = 64MB,
        [long]$ReserveBytes = 2GB
    )
    if ($MaximumRetainedEvidenceBytes -lt 0 -or $ReserveBytes -lt 0) {
        throw 'Post-operation storage limits cannot be negative.'
    }

    $hostGrowth = [Math]::Max([long]0, $HostFreeBefore - $HostFreeAfter)
    $retainedEvidenceBytes = Get-AdvertifiedPathSizeBytes -Path $EvidencePath
    Write-Host ("Verifier retained evidence: {0:N2} MiB; host-wide free-space movement: {1:N2} MiB." -f
        ($retainedEvidenceBytes / 1MB), ($hostGrowth / 1MB))

    if ($retainedEvidenceBytes -gt $MaximumRetainedEvidenceBytes) {
        throw 'Verification retained more than 64 MiB of evidence; inspect the verifier output before continuing.'
    }
    if ($HostFreeAfter -lt $ReserveBytes) {
        throw 'Verification left less than the required host storage reserve; reclaim only disposable build/test artifacts before continuing.'
    }
    if ($hostGrowth -gt $MaximumRetainedEvidenceBytes) {
        Write-Warning ("Host-wide free space dropped by {0:N2} MiB during verification. This is retained as an operational signal but is not attributed to Advertified unless verifier-owned retained evidence exceeds its bound." -f ($hostGrowth / 1MB))
    }

    return [pscustomobject]@{
        HostGrowthBytes = $hostGrowth
        RetainedEvidenceBytes = $retainedEvidenceBytes
        HostGrowthAttributedToVerifier = ($retainedEvidenceBytes -gt $MaximumRetainedEvidenceBytes)
    }
}

function Test-AdvertifiedDockerImageExists {
    param([Parameter(Mandatory = $true)][string]$Image)
    # A missing image is the expected negative result of this probe. Windows
    # PowerShell otherwise promotes Docker's stderr to a terminating error when
    # the caller uses ErrorActionPreference=Stop, skipping the storage allowance.
    $previousErrorAction = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & docker image inspect $Image *> $null
        return $LASTEXITCODE -eq 0
    }
    finally {
        $ErrorActionPreference = $previousErrorAction
    }
}

if ($Report) {
    $ErrorActionPreference = 'Stop'
    $freeBytes = Get-AdvertifiedHostFreeBytes
    Write-Host ("Host free bytes: {0}; free GiB: {1:N2}" -f $freeBytes, ($freeBytes / 1GB))
    Assert-AdvertifiedStorageHeadroom
    & docker system df
    if ($LASTEXITCODE -ne 0) {
        throw 'Docker storage accounting is unavailable; no build or cleanup was attempted.'
    }
}
