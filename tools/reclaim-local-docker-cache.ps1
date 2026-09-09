param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$before = (Get-PSDrive C).Free
Write-Host ("Host storage before reclaim: {0:N2} GiB free." -f ($before / 1GB))

# Reclaim only the disposable validation image, unused build cache and dangling images.
# Never prune volumes, containers, networks, or named application images from this path.
& docker image rm advertified/api-validation:local 2>$null
if ($LASTEXITCODE -notin @(0, 1)) { throw 'Disposable validation-image reclaim failed.' }
& docker builder prune --all --force
if ($LASTEXITCODE -ne 0) { throw 'Docker builder cache reclaim failed.' }
& docker image prune --force
if ($LASTEXITCODE -ne 0) { throw 'Docker dangling-image reclaim failed.' }

$after = (Get-PSDrive C).Free
Write-Host ("Host storage after reclaim: {0:N2} GiB free; reclaimed {1:N2} GiB." -f
    ($after / 1GB), (($after - $before) / 1GB))
