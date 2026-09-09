# Host reserve is operational safety headroom, not a promise about other applications.
function Get-AdvertifiedHostFreeBytes {
    $workspacePath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
    $drive = [System.IO.DriveInfo]::new([System.IO.Path]::GetPathRoot($workspacePath))
    return $drive.AvailableFreeSpace
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

function Test-AdvertifiedDockerImageExists {
    param([Parameter(Mandatory = $true)][string]$Image)
    & docker image inspect $Image *> $null
    return $LASTEXITCODE -eq 0
}
