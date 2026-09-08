param(
    [ValidateRange(10, 300)]
    [int]$Tail = 120,

    [ValidateSet("all", "agent-runtime", "api", "web")]
    [string]$Service = "all"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$project = "advertified-os2-dev"
$services = if ($Service -eq "all") {
    @("agent-runtime", "api", "web")
} else {
    @($Service)
}
$containers = & docker ps --all --filter "label=com.docker.compose.project=$project" --format "{{.Names}}"
if ($LASTEXITCODE -ne 0 -or -not $containers) {
    throw "The Advertified development project is unavailable."
}
if ($containers | Where-Object { $_ -match "(?i)prod" }) {
    throw "Refusing to read a production-labelled container."
}

$previousErrorPreference = $ErrorActionPreference
$ErrorActionPreference = "Continue"
Write-Output "[configured-preview-models]"
& docker exec "$project-api-1" grep -n "amazon.nova-lite" /app/appsettings.Development.json 2>&1
Write-Output "[configured-runtime-allowlist]"
& docker exec "$project-agent-runtime-1" printenv ADVERTIFIED_BEDROCK_MODEL_ALLOWLIST 2>&1
$ErrorActionPreference = $previousErrorPreference

foreach ($service in $services) {
    $container = "$project-$service-1"
    if ($containers -notcontains $container) {
        Write-Output "[$container] unavailable"
        continue
    }
    $previousErrorPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    $diagnostic = (& docker logs --tail $Tail $container 2>&1 | Out-String)
    $ErrorActionPreference = $previousErrorPreference
    $diagnostic = [regex]::Replace(
        $diagnostic,
        "[\x00-\x08\x0B\x0C\x0E-\x1F]",
        "")
    $diagnostic = $diagnostic.Replace(
        "advertified-agent-runtime-local-only",
        "[REDACTED]")
    $diagnostic = $diagnostic.Replace(
        "advertified-local-only",
        "[REDACTED]")
    Write-Output "[$container]"
    Write-Output $diagnostic
}
