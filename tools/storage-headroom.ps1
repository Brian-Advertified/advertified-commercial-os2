# A host reserve is operational headroom, not a promise about other applications.
function Assert-AdvertifiedStorageHeadroom {
    param([long]$ExpectedGrowthBytes = 2GB, [long]$ReserveBytes = 5GB)
    $workspacePath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
    $drive = [System.IO.DriveInfo]::new([System.IO.Path]::GetPathRoot($workspacePath))
    $available = $drive.AvailableFreeSpace
    Write-Host ("Host storage: {0:N2} GiB free; {1:N2} GiB reserve; {2:N2} GiB operation allowance." -f
        ($available / 1GB), ($ReserveBytes / 1GB), ($ExpectedGrowthBytes / 1GB))
    if ($available -lt ($ReserveBytes + $ExpectedGrowthBytes)) {
        throw 'Insufficient host storage headroom. Recover unused cache/offline Docker disk space before building; do not delete data volumes.'
    }
}
