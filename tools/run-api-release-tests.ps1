param(
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$Filter,
    [string]$IntegrationFilter
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
# Explicit Docker-pinned validation only: never kill host processes or launch a stack.
# The build is socket-free. An explicitly selected integration run below uses
# disposable Testcontainers databases, never the application's running database.
& docker build --file (Join-Path $repoRoot 'api/Dockerfile') --target tests `
    --build-arg "TEST_FILTER=$Filter" --tag advertified/api-validation:local --progress plain $repoRoot
if ($LASTEXITCODE -ne 0) {
    throw "Docker-pinned API validation failed with exit code $LASTEXITCODE."
}
if ($IntegrationFilter) {
    & docker run --rm --mount type=bind,source=/var/run/docker.sock,target=/var/run/docker.sock `
        --add-host host.docker.internal:host-gateway -e TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal `
        advertified/api-validation:local dotnet test `
        api/tests/Advertified.Commercial.Api.Tests/Advertified.Commercial.Api.Tests.csproj `
        --configuration Release --no-build --no-restore --filter $IntegrationFilter --logger 'console;verbosity=detailed'
    if ($LASTEXITCODE -ne 0) { throw "Disposable API integration validation failed with exit code $LASTEXITCODE." }
}
