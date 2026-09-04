$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repoRoot
$project = 'advertified-os2-dev'
$container = 'advertified-os2-dev-web-1'

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath exited with code $LASTEXITCODE."
    }
}

$labels = docker inspect $container --format '{{json .Config.Labels}}' |
    ConvertFrom-Json
if ($labels.'com.docker.compose.project' -ne $project) {
    throw 'The existing web container is not part of advertified-os2-dev.'
}
$configFiles = @($labels.'com.docker.compose.project.config_files' -split ',') |
    ForEach-Object { $_.Trim() } |
    Where-Object {
        $_ -and
        $_ -notlike '*docker-compose.bedrock.override.yml' -and
        $_ -notlike '*docker-compose.canary.override.yml'
    }
if ($configFiles.Count -eq 0) {
    throw 'Unable to recover the OS2 Compose configuration.'
}
$arguments = @('compose', '-p', $project)
foreach ($file in $configFiles) { $arguments += @('-f', $file) }

Invoke-Checked -FilePath 'docker' -Arguments (
    $arguments + @('build', 'web')
)
Invoke-Checked -FilePath 'docker' -Arguments (
    $arguments + @(
        'up', '-d', '--no-deps', '--force-recreate', 'web'
    )
)
for ($attempt = 1; $attempt -le 90; $attempt++) {
    try {
        $response = Invoke-WebRequest -UseBasicParsing `
            -Uri 'http://127.0.0.1:3017/' -TimeoutSec 5
        if ($response.StatusCode -eq 200) { break }
    }
    catch { Start-Sleep -Seconds 2 }
    if ($attempt -eq 90) { throw 'Updated OS2 web did not become healthy.' }
}
Invoke-Checked -FilePath 'docker' -Arguments @('image', 'prune', '-f')
Write-Host 'The existing advertified-os2-dev web service was replaced successfully.'
