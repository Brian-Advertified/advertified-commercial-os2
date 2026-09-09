$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$infra = @(
    'advertified-os2-dev-postgres-1',
    'advertified-os2-dev-minio-1',
    'advertified-os2-dev-clamav-1',
    'advertified-os2-dev-redis-1',
    'advertified-os2-dev-mailhog-1'
)
$app = @(
    'advertified-os2-dev-agent-runtime-1',
    'advertified-os2-dev-api-1',
    'advertified-os2-dev-web-1'
)

function Assert-ContainerExists([string]$Name) {
    & docker container inspect $Name *> $null
    if ($LASTEXITCODE -ne 0) { throw "Expected local Advertified container is missing: $Name" }
}

function Start-Existing([string]$Name) {
    Assert-ContainerExists $Name
    & docker start $Name | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to start $Name" }
}

function Wait-ContainerReady([string]$Name) {
    for ($attempt = 1; $attempt -le 90; $attempt++) {
        $state = (& docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' $Name).Trim()
        if ($LASTEXITCODE -eq 0 -and ($state -eq 'healthy' -or $state -eq 'running')) {
            Write-Host "$Name is $state."
            return
        }
        if ($state -eq 'unhealthy' -or $state -eq 'exited' -or $state -eq 'dead') {
            throw "$Name entered state $state while restoring the local stack."
        }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Name to become ready."
}

foreach ($name in $infra) { Start-Existing $name }
foreach ($name in $infra) { Wait-ContainerReady $name }
foreach ($name in $app) {
    Start-Existing $name
    Wait-ContainerReady $name
}

Write-Host 'Advertified local containers restored without running migrator or development seed.'
