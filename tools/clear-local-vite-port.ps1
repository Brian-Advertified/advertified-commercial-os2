param(
    [int]$Port = 3017
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-ProcessRecord([int]$ProcessId) {
    return Get-CimInstance Win32_Process -Filter "ProcessId = $ProcessId" -ErrorAction SilentlyContinue
}

function Is-VerifiedViteListener($Process, [int]$ExpectedPort) {
    if (-not $Process) { return $false }
    $commandLine = [string]$Process.CommandLine
    return $Process.Name -ieq 'node.exe' -and
        $commandLine -match '(?i)vite' -and
        $commandLine -match "(?<!\d)$ExpectedPort(?!\d)"
}

function Is-VerifiedDevAncestor($Process, [int]$ExpectedPort) {
    if (-not $Process) { return $false }
    $commandLine = [string]$Process.CommandLine
    $mentionsPort = $commandLine -match "(?<!\d)$ExpectedPort(?!\d)"
    $viteLauncher = $commandLine -match '(?i)vite' -and $mentionsPort
    $npmDevLauncher = $commandLine -match '(?i)npm-cli\.js' -and
        $commandLine -match '(?i)\brun\s+dev\b'
    $connectedPlaywright = $Process.Name -ieq 'node.exe' -and
        $commandLine -match '(?i)@playwright' -and
        $commandLine -match '(?i)playwright\.connected-current\.config\.ts'
    return $viteLauncher -or $npmDevLauncher -or $connectedPlaywright
}

for ($attempt = 1; $attempt -le 4; $attempt++) {
    $listeners = @(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue)
    if ($listeners.Count -eq 0) {
        Write-Host "Local port $Port is free."
        exit 0
    }

    $pids = @($listeners | Select-Object -ExpandProperty OwningProcess -Unique)
    foreach ($processId in $pids) {
        $listenerProcess = Get-ProcessRecord $processId
        if (-not (Is-VerifiedViteListener $listenerProcess $Port)) {
            throw "Refusing to stop PID $processId on port $Port because it is not a verified Vite node process."
        }

        $root = $listenerProcess
        $cursor = $listenerProcess
        for ($depth = 0; $depth -lt 6; $depth++) {
            $parentId = [int]$cursor.ParentProcessId
            if ($parentId -le 0) { break }
            $parent = Get-ProcessRecord $parentId
            if (-not (Is-VerifiedDevAncestor $parent $Port)) { break }
            $root = $parent
            $cursor = $parent
        }

        Write-Host "Stopping verified Vite process tree rooted at PID $($root.ProcessId) for local port $Port."
        & taskkill.exe /PID $root.ProcessId /T /F | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to stop the verified Vite process tree rooted at PID $($root.ProcessId)."
        }
    }

    Start-Sleep -Milliseconds 750
}

$remaining = @(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue)
if ($remaining.Count -ne 0) {
    throw "Port $Port is still in use after guarded Vite process-tree cleanup."
}
Write-Host "Local port $Port is free."
