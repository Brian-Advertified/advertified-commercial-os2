$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$dockerDesktopExe = 'C:\Program Files\Docker\Docker\Docker Desktop.exe'
if (-not (Test-Path -LiteralPath $dockerDesktopExe)) {
    throw 'Docker Desktop executable was not found.'
}

try {
    & docker info *> $null
    if ($LASTEXITCODE -eq 0) {
        Write-Host 'Docker Desktop is already ready.'
        exit 0
    }
}
catch { }

Write-Host 'Starting Docker Desktop...'
Start-Process -FilePath $dockerDesktopExe | Out-Null
for ($attempt = 1; $attempt -le 90; $attempt++) {
    Start-Sleep -Seconds 2
    try {
        & docker info *> $null
        if ($LASTEXITCODE -eq 0) {
            Write-Host 'Docker Desktop is ready.'
            exit 0
        }
    }
    catch { }
}
throw 'Docker Desktop did not become ready.'
