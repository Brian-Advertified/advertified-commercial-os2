param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Enable", "Disable")]
    [string]$Mode,

    [ValidatePattern("^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")]
    [string]$AwsProfile = "advertified-codex-audit"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$coreCompose = Join-Path $repoRoot "infrastructure/docker-compose.yml"
$baseCompose = Join-Path $repoRoot "infrastructure/docker-compose.app.yml"
$bedrockCompose = Join-Path $repoRoot "infrastructure/development/docker-compose.bedrock-preview.yml"
$project = "advertified-os2-dev"
$expectedContainers = @(
    "$project-agent-runtime-1",
    "$project-api-1",
    "$project-web-1"
)

$existing = & docker ps --filter "label=com.docker.compose.project=$project" --format "{{.Names}}"
if ($LASTEXITCODE -ne 0 -or -not $existing) {
    throw "The existing Advertified development Compose project is not running."
}
if ($existing | Where-Object { $_ -match "(?i)prod" }) {
    throw "Refusing to modify a production-labelled container."
}

# A cancelled Compose replacement can leave project-scoped Created or Exited
# containers holding canonical service names. Remove only those disposable
# containers; named data volumes are never removed.
$staleContainerIds = & docker ps --all --filter "label=com.docker.compose.project=$project" --filter "status=created" --filter "status=exited" --format "{{.ID}}"
if ($LASTEXITCODE -ne 0) {
    throw "Unable to inspect stale Advertified development containers."
}
if ($staleContainerIds) {
    & docker rm @staleContainerIds
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to remove stale Advertified development containers."
    }
}

$composeArguments = @(
    "compose", "--project-name", $project,
    "--file", $coreCompose,
    "--file", $baseCompose
)
if ($Mode -eq "Enable") {
    if (-not (Test-Path $bedrockCompose)) {
        throw "The governed Bedrock preview override is unavailable."
    }
    $env:AWS_PROFILE = $AwsProfile
    $composeArguments += @("--file", $bedrockCompose)
}
$composeArguments += @(
    "up",
    "--detach",
    "--no-build",
    "--force-recreate",
    "agent-runtime",
    "api",
    "web"
)

& docker @composeArguments
if ($LASTEXITCODE -ne 0) {
    foreach ($service in @("migrator", "agent-runtime", "api", "web")) {
        $container = "$project-$service-1"
        $previousErrorPreference = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        $diagnostic = (& docker logs --tail 80 $container 2>&1 | Out-String)
        $ErrorActionPreference = $previousErrorPreference
        $diagnostic = [regex]::Replace($diagnostic, "[\x00-\x08\x0B\x0C\x0E-\x1F]", "")
        $diagnostic = $diagnostic.Replace("advertified-local-only", "[REDACTED]")
        $diagnostic = $diagnostic.Replace("advertified-agent-runtime-local-only", "[REDACTED]")
        Write-Output "[$container]"
        Write-Output $diagnostic
    }
    throw "Advertified $Mode configuration failed."
}

$deadline = [DateTime]::UtcNow.AddMinutes(4)
do {
    $unhealthy = @()
    foreach ($container in $expectedContainers) {
        $state = & docker inspect $container --format "{{.State.Status}}|{{if .State.Health}}{{.State.Health.Status}}{{end}}"
        if ($LASTEXITCODE -ne 0 -or $state -notmatch "^running\|healthy$") {
            $unhealthy += $container
        }
    }
    if ($unhealthy.Count -eq 0) {
        Write-Output "Advertified preview runtime mode: $Mode"
        exit 0
    }
    Start-Sleep -Seconds 2
} while ([DateTime]::UtcNow -lt $deadline)

throw "Advertified $Mode configuration did not become healthy: $($unhealthy -join ', ')."
