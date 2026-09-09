param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$before = (Get-PSDrive C).Free
Write-Host ("Host storage before reclaim: {0:N2} GiB free." -f ($before / 1GB))

# Remove only stopped disposable validation containers that pin the validation image.
# Running validation containers are left alone so concurrent tests are never killed.
$stoppedValidation = @(& docker ps -aq --filter ancestor=advertified/api-validation:local --filter status=exited)
if ($LASTEXITCODE -ne 0) { throw 'Unable to inspect disposable validation containers.' }
foreach ($containerId in $stoppedValidation) {
    if ($containerId) {
        & docker rm $containerId *> $null
        if ($LASTEXITCODE -ne 0) { throw "Could not remove stopped validation container $containerId." }
    }
}

$runningValidation = @(& docker ps -q --filter ancestor=advertified/api-validation:local)
if ($LASTEXITCODE -ne 0) { throw 'Unable to inspect running validation containers.' }
if ($runningValidation.Count -eq 0) {
    $validationImageId = (& docker images -q advertified/api-validation:local | Select-Object -First 1)
    if ($LASTEXITCODE -ne 0) { throw 'Unable to inspect the disposable validation image.' }
    if ($validationImageId) {
        & docker image rm advertified/api-validation:local *> $null
        if ($LASTEXITCODE -ne 0) { throw 'Disposable validation-image reclaim failed.' }
    }
}
else {
    Write-Host 'A validation test container is currently running; keeping its image until the test exits.'
}

# Keep cache bounded. Never prune volumes, containers, networks, or named application images.
& docker builder prune --force --max-used-space 1GB --reserved-space 256MB
if ($LASTEXITCODE -ne 0) { throw 'Docker builder cache reclaim failed.' }
& docker image prune --force
if ($LASTEXITCODE -ne 0) { throw 'Docker dangling-image reclaim failed.' }

$after = (Get-PSDrive C).Free
Write-Host ("Host storage after reclaim: {0:N2} GiB free; reclaimed {1:N2} GiB." -f
    ($after / 1GB), (($after - $before) / 1GB))
