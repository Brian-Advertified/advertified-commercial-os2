param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Owner-authorised maintenance for this exact local Docker disk. This script never deletes or moves
# the VHDX, Docker volumes, databases, or application data. It temporarily stops Docker/WSL so the
# sparse VHDX can be compacted, then restores the previously running Advertified containers.
$dockerDisk = 'C:\Users\CC KEMPTON\AppData\Local\Docker\wsl\disk\docker_data.vhdx'
$dockerDesktopExe = 'C:\Program Files\Docker\Docker\Docker Desktop.exe'
$composeProject = 'advertified-os2-dev'
$reportDirectory = Join-Path $PSScriptRoot '../.artifacts/storage-maintenance'
$reportPath = Join-Path $reportDirectory 'compaction-result.json'
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null

$result = [ordered]@{
    status = 'running'
    beforeFreeBytes = (Get-PSDrive C).Free
    beforeDiskBytes = $null
    afterDiskBytes = $null
    afterFreeBytes = $null
    restoredContainers = @()
}
$runningContainers = @()
$dockerServiceWasRunning = $false
$maintenanceStarted = $false

function Test-Administrator {
    $principal = [Security.Principal.WindowsPrincipal]::new(
        [Security.Principal.WindowsIdentity]::GetCurrent())
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-DockerReady {
    try {
        & docker info *> $null
        return $LASTEXITCODE -eq 0
    }
    catch {
        return $false
    }
}

function Wait-DockerReady {
    for ($attempt = 1; $attempt -le 60; $attempt++) {
        if (Test-DockerReady) { return }
        Start-Sleep -Seconds 2
    }
    throw 'Docker Desktop did not become ready after maintenance.'
}

function Wait-DockerDiskDetached([string]$Path) {
    for ($attempt = 1; $attempt -le 60; $attempt++) {
        $disk = Get-VHD -Path $Path
        if (-not $disk.Attached) { return }
        Start-Sleep -Seconds 1
    }
    throw 'Docker disk remained attached after Docker Desktop and WSL were stopped.'
}

function Restore-Docker {
    $service = Get-Service -Name 'com.docker.service' -ErrorAction SilentlyContinue
    if ($dockerServiceWasRunning -and $service -and $service.Status -ne 'Running') {
        Start-Service -Name 'com.docker.service'
    }
    if (-not (Test-DockerReady)) {
        if (-not (Test-Path -LiteralPath $dockerDesktopExe)) {
            throw 'Docker Desktop executable was not found for restart.'
        }
        Start-Process -FilePath $dockerDesktopExe | Out-Null
        Wait-DockerReady
    }
    if ($runningContainers.Count -gt 0) {
        & docker start @runningContainers | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Failed to restore one or more Advertified containers.' }
        $result.restoredContainers = @($runningContainers)
    }
}

try {
    if (-not (Test-Administrator)) {
        throw 'Windows administrator access is required for Docker disk compaction.'
    }

    $resolved = (Resolve-Path -LiteralPath $dockerDisk).Path
    if ($resolved -ne $dockerDisk) { throw 'Unexpected Docker disk path.' }
    $result.beforeDiskBytes = (Get-Item -LiteralPath $resolved).Length

    if (Test-DockerReady) {
        $runningContainers = @(
            & docker ps --filter "label=com.docker.compose.project=$composeProject" --format '{{.Names}}'
        )
        if ($LASTEXITCODE -ne 0) { throw 'Unable to inspect running Advertified containers.' }
        if ($runningContainers.Count -gt 0) {
            Write-Host "Stopping $($runningContainers.Count) running Advertified containers without removing them or their volumes..."
            & docker stop @runningContainers | Out-Null
            if ($LASTEXITCODE -ne 0) { throw 'Failed to stop the running Advertified containers.' }
        }
    }

    $service = Get-Service -Name 'com.docker.service' -ErrorAction SilentlyContinue
    $dockerServiceWasRunning = $service -and $service.Status -eq 'Running'
    $maintenanceStarted = $true

    Write-Host 'Stopping Docker Desktop processes...'
    Get-Process -Name 'Docker Desktop','com.docker.backend','com.docker.build' -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue
    if ($service -and $service.Status -ne 'Stopped') {
        Stop-Service -Name 'com.docker.service' -Force
    }

    Write-Host 'Shutting down WSL so Docker virtual-disk blocks can be released...'
    & wsl.exe --shutdown
    if ($LASTEXITCODE -ne 0) { throw 'WSL shutdown failed.' }

    Wait-DockerDiskDetached $resolved
    Write-Host 'Compacting Docker Desktop virtual disk...'
    Optimize-VHD -Path $resolved -Mode Full

    $result.afterDiskBytes = (Get-Item -LiteralPath $resolved).Length
    $result.afterFreeBytes = (Get-PSDrive C).Free

    Write-Host 'Restarting Docker Desktop and restoring the previously running Advertified containers...'
    Restore-Docker
    $result.status = 'completed'
}
catch {
    $result.status = 'failed'
    $result.error = $_.Exception.Message
}
finally {
    if ($maintenanceStarted -and $result.status -ne 'completed') {
        try {
            Write-Host 'Maintenance failed; restoring Docker Desktop and the prior Advertified container state...'
            Restore-Docker
        }
        catch {
            $result.restoreError = $_.Exception.Message
        }
    }
    $result.afterFreeBytes = (Get-PSDrive C).Free
    $result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $reportPath -Encoding UTF8
}

if ($result.status -ne 'completed') { exit 1 }
Write-Host ("Docker disk compacted: {0:N2} GiB -> {1:N2} GiB; C: free {2:N2} GiB -> {3:N2} GiB." -f
    ($result.beforeDiskBytes / 1GB), ($result.afterDiskBytes / 1GB),
    ($result.beforeFreeBytes / 1GB), ($result.afterFreeBytes / 1GB))
