$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$testProject = Join-Path $repoRoot `
    'api\tests\Advertified.Commercial.Api.Tests\Advertified.Commercial.Api.Tests.csproj'

Get-CimInstance Win32_Process -Filter "Name = 'testhost.exe'" |
    Where-Object {
        $_.CommandLine -and
        $_.CommandLine.IndexOf(
            $repoRoot,
            [StringComparison]::OrdinalIgnoreCase
        ) -ge 0
    } |
    ForEach-Object {
        Write-Host "Stopping stale repository testhost PID $($_.ProcessId)..."
        Stop-Process -Id $_.ProcessId -Force -ErrorAction Stop
    }

Push-Location (Join-Path $repoRoot 'api')
try {
    dotnet test $testProject -c Release --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "API Release tests failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
